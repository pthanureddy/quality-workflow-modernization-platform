using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QualityWorkflow.Api.Contracts;
using QualityWorkflow.Api.Data;
using QualityWorkflow.Api.Domain;

namespace QualityWorkflow.Api.Services;

public sealed class LegacyProcedureImporter(QualityDbContext dbContext)
{
    private static readonly string[] RequiredColumns =
        ["legacy_id", "title", "owner", "version", "status", "review_due_on"];

    public async Task<ImportResult> ImportAsync(Stream csvStream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(csvStream, Encoding.UTF8, true, leaveOpen: true);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (headerLine is null)
        {
            throw new WorkflowValidationException("The import file is empty.");
        }

        var headers = CsvLineParser.Parse(headerLine)
            .Select((value, index) => new { Name = value.Trim().ToLowerInvariant(), Index = index })
            .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);

        var missingColumns = RequiredColumns.Where(column => !headers.ContainsKey(column)).ToArray();
        if (missingColumns.Length > 0)
        {
            throw new WorkflowValidationException(
                $"Missing required columns: {string.Join(", ", missingColumns)}.");
        }

        var existing = await dbContext.Procedures
            .ToDictionaryAsync(x => x.LegacyId, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var issues = new List<ImportIssue>();
        var created = 0;
        var updated = 0;
        var rowNumber = 1;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            rowNumber += 1;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = CsvLineParser.Parse(line);
            string Value(string name) => headers[name] < fields.Count ? fields[headers[name]].Trim() : string.Empty;

            var legacyId = Value("legacy_id");
            var title = Value("title");
            var owner = Value("owner");
            var version = Value("version");

            if (string.IsNullOrWhiteSpace(legacyId) || string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(version))
            {
                issues.Add(new ImportIssue(rowNumber, "Legacy ID, title, owner, and version are required."));
                continue;
            }

            if (!Enum.TryParse<ProcedureStatus>(Value("status"), true, out var status))
            {
                issues.Add(new ImportIssue(rowNumber, "Status must be Draft, Active, or Archived."));
                continue;
            }

            if (!DateOnly.TryParseExact(
                    Value("review_due_on"),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var reviewDueOn))
            {
                issues.Add(new ImportIssue(rowNumber, "Review date must use yyyy-MM-dd."));
                continue;
            }

            if (existing.TryGetValue(legacyId, out var procedure))
            {
                procedure.Title = title;
                procedure.Owner = owner;
                procedure.VersionLabel = version;
                procedure.Status = status;
                procedure.ReviewDueOn = reviewDueOn;
                procedure.Revision += 1;
                procedure.UpdatedAt = DateTimeOffset.UtcNow;
                updated += 1;
                AddAudit(procedure, "UpdatedFromLegacyImport");
            }
            else
            {
                procedure = new ProcedureRecord
                {
                    LegacyId = legacyId,
                    Title = title,
                    Owner = owner,
                    VersionLabel = version,
                    Status = status,
                    ReviewDueOn = reviewDueOn
                };
                dbContext.Procedures.Add(procedure);
                existing.Add(legacyId, procedure);
                created += 1;
                AddAudit(procedure, "CreatedFromLegacyImport");
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ImportResult(created, updated, issues.Count, issues);
    }

    private void AddAudit(ProcedureRecord procedure, string action)
    {
        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = nameof(ProcedureRecord),
            EntityId = procedure.Id,
            Action = action,
            Detail = $"Legacy ID {procedure.LegacyId}; version {procedure.VersionLabel}; status {procedure.Status}"
        });
    }
}

internal static class CsvLineParser
{
    public static IReadOnlyList<string> Parse(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < line.Length; index += 1)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index += 1;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        if (quoted)
        {
            throw new WorkflowValidationException("A CSV row contains an unclosed quoted value.");
        }

        fields.Add(current.ToString());
        return fields;
    }
}

