# Requirement-to-test traceability

| Requirement | Implementation | Verification |
| --- | --- | --- |
| Import controlled procedures from a legacy export | `LegacyProcedureImporter` validates the required CSV schema and upserts by legacy ID | Importer unit tests and HTTP multipart import test |
| Preserve commas and quotes in exported text | `CsvLineParser` handles quoted values and escaped quotes | Quoted-title import test and malformed-quote test |
| Reject records that cannot be migrated safely | Row validation checks IDs, required text, status, and ISO date format | Invalid-status and missing-column tests |
| Keep a trace of migrated and changed records | Every accepted import and workflow transition creates an `AuditEntry` | Import audit and status-change audit assertions |
| Prevent duplicate procedure records on repeat import | Unique database index plus importer upsert by `LegacyId` | Create/update importer test |
| Prevent stale browser updates from overwriting newer data | `Revision` is an EF concurrency token and is required in status updates | Service conflict test and HTTP 409 integration test |
| Allow only reviewed workflow transitions | Draft -> Active -> Archived transition policy in the workflow service | Three invalid-transition cases and one accepted-transition test |
| Show process health at a glance | Dashboard queries state counts, overdue reviews, and open corrective actions | Dashboard integration test and React summary rendering test |
| Support modern web use | React/TypeScript register with search, status filtering, CSV upload, and state actions | Vitest rendering/filtering and workflow-action tests; production build |
| Run on SQL Server | SQL Server provider, EF migration, compose environment, and CI service container | CI SQL Server migration and round-trip test |

The verified inventory is 14 backend tests (including three theory cases, a SQLite seed regression test, and one SQL Server provider test) and 2 React tests.
