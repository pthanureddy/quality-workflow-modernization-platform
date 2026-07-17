using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QualityWorkflow.Api.Data;
using QualityWorkflow.Api.Domain;

namespace QualityWorkflow.Api.Tests;

public sealed class SqlServerProviderTests
{
    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task SqlServer_migrations_and_round_trip_use_the_real_provider()
    {
        var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await WaitForSqlServerAsync(connectionString);
        var options = new DbContextOptionsBuilder<QualityDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var dbContext = new QualityDbContext(options);
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
        dbContext.Procedures.Add(new ProcedureRecord
        {
            LegacyId = "SQL-001",
            Title = "SQL Server provider verification",
            Owner = "CI",
            VersionLabel = "1.0",
            Status = ProcedureStatus.Active,
            ReviewDueOn = new DateOnly(2026, 12, 31)
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var stored = await dbContext.Procedures.SingleAsync(x => x.LegacyId == "SQL-001");

        Assert.Equal("SQL Server provider verification", stored.Title);
        Assert.Equal(ProcedureStatus.Active, stored.Status);
        Assert.Equal(1, stored.Revision);
    }

    private static async Task WaitForSqlServerAsync(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master",
            ConnectTimeout = 3
        };

        Exception? lastError = null;
        for (var attempt = 0; attempt < 20; attempt += 1)
        {
            try
            {
                await using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync();
                return;
            }
            catch (Exception exception) when (exception is SqlException or InvalidOperationException)
            {
                lastError = exception;
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        throw new InvalidOperationException("SQL Server did not become ready within 60 seconds.", lastError);
    }
}

