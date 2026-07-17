# Architecture and design decisions

## System boundary

The application models one modernization slice: moving controlled procedure records from a legacy CSV export into a web workflow with relational persistence, explicit state transitions, audit evidence, and a searchable React interface.

```text
Legacy CSV export                         Browser
       |                                    |
       v                                    v
CSV schema validation -> ASP.NET Core Web API <- React + TypeScript
                              |
             +----------------+----------------+
             |                |                |
             v                v                v
      Import service    Workflow service   Dashboard queries
             |                |                |
             +----------------+----------------+
                              |
                         EF Core 8
                              |
                  +-----------+-----------+
                  |                       |
             SQL Server 2022          SQLite
             deployed path        local/test path
```

## Module responsibilities

- `Domain`: controlled procedures, corrective actions, audit entries, and workflow statuses.
- `Data`: EF Core mappings, provider-neutral migrations, indexes, and seed data.
- `LegacyProcedureImporter`: CSV schema checks, row-level validation, idempotent upserts, and import audit entries.
- `ProcedureWorkflowService`: legal state transitions, optimistic concurrency through a revision token, and change auditing.
- Minimal API routes: HTTP validation and serialization only; domain decisions remain in services.
- React client: register filtering, dashboard counts, CSV upload, and revision-aware state changes.

## Database decisions

SQL Server is the deployed provider path and applies the checked-in EF Core migration. SQLite keeps local setup and provider-independent tests fast and creates its schema directly from the same EF Core model. The schema uses:

- a unique index on legacy procedure IDs to prevent duplicate migration records;
- indexes on procedure status/review date and action status/due date for dashboard queries;
- a foreign key from corrective actions to procedures;
- a concurrency token on `Revision` so stale browser updates receive HTTP 409;
- an append-only audit table for import and workflow events.

The SQL Server CI job starts SQL Server 2022, applies the real migration, inserts a procedure, clears the EF tracking state, and reads the row back through the SQL Server provider.

## Error handling

Known validation, conflict, and not-found failures are translated to RFC 7807 problem responses. Unexpected exceptions return a generic 500 response rather than exposing server details. The import endpoint rejects empty files, files over 1 MB, missing columns, invalid statuses, invalid dates, and malformed quoted CSV values.

## Trade-offs and limits

- Authentication and role-based approval are outside this repository; a production workflow must bind approvals and tenant scope to identity claims.
- CSV is a migration seam, not a permanent integration contract. A production cutover should use signed export manifests and reconciliation reports.
- Audit rows are application-generated. Regulated environments may also require database-level immutability, retention policy, and trusted timestamps.
- Corrective actions are represented in the database and dashboard but do not yet have create/update HTTP routes.
- The revision token prevents lost updates. SQL Server `rowversion` could replace it if all supported database providers can use provider-specific concurrency behavior.
