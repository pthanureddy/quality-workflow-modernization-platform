# Legacy data migration plan

## 1. Profile the source

Before cutover, record row counts, null rates, duplicate legacy IDs, status values, date formats, maximum field lengths, character encodings, and relationships to attachments or approvals. The sample importer assumes UTF-8 CSV and the explicit schema documented below.

| Source column | Target field | Rule |
| --- | --- | --- |
| `legacy_id` | `ProcedureRecord.LegacyId` | Required; trimmed; unique; maximum 40 characters |
| `title` | `ProcedureRecord.Title` | Required; maximum 200 characters |
| `owner` | `ProcedureRecord.Owner` | Required; maximum 100 characters |
| `version` | `ProcedureRecord.VersionLabel` | Required; maximum 30 characters |
| `status` | `ProcedureRecord.Status` | Draft, Active, or Archived |
| `review_due_on` | `ProcedureRecord.ReviewDueOn` | ISO date in `yyyy-MM-dd` format |

## 2. Rehearse

1. Restore a masked production export into an isolated migration environment.
2. Apply the EF Core migration to an empty SQL Server database.
3. Import the export and retain row-level rejections.
4. Reconcile source and target counts by status and owner.
5. Sample records with quotes, non-ASCII text, long titles, and repeated IDs.
6. Run API, workflow, and dashboard checks against the migrated database.

## 3. Cut over

1. Announce a write freeze for the legacy system.
2. Produce a final export plus a checksum and row-count manifest.
3. Back up the target database.
4. Apply pending migrations and run the importer.
5. Block release if any rejection is unexplained or reconciliation differs.
6. Switch users to the web application and monitor error, latency, and audit-event rates.

## 4. Roll back

Keep the legacy system read-only and retain the pre-cutover SQL Server backup. If reconciliation or smoke checks fail, restore the backup, return writes to the legacy product, and preserve the failed migration database for diagnosis. Do not edit rejected rows directly in the target; correct the mapping or source export and repeat the rehearsal.

