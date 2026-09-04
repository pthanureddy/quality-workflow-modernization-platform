# Architecture and design decisions

## System boundary

The application models one modernization slice: moving controlled procedure records from a legacy CSV export into a web workflow with relational persistence, explicit state transitions, audit evidence, and a searchable React interface.

```text
Legacy CSV export -> Browser / React + TypeScript UI
                              |
                 +------------+--------------------+
                 |                                 |
        status Web Component                 REST adapter
           (Shadow DOM)                   /               \
                                    CSV preflight    runtime parsers
                                                   |
                                                   v
                                      ASP.NET Core Web API
                                                   |
                              +--------------------+-------------------+
                              |                    |                   |
                       Import service       Workflow service    Dashboard queries
                              |                    |                   |
                              +--------------------+-------------------+
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
- `App.tsx`: page composition, deferred register filtering, dashboard counts, CSV workflow, loading/ready/unavailable refresh state, focus management, live announcements, and revision-aware state changes. A confirmed status update is applied and announced before the dashboard/register refresh, so a failed follow-up read does not misreport the completed write as failed.
- `api.ts`: the REST transport boundary, problem-detail handling, and client-side CSV preflight checks.
- `contract.ts`: runtime parsers that accept `unknown`, reject malformed payloads, and produce typed dashboard, procedure, and import-result objects.
- `workflow-status-badge.ts`: the React-free native `workflow-status-badge` custom element. Its Shadow DOM owns badge rendering and styling, allowing use outside React.
- `WorkflowStatusBadge.tsx`: the thin React wrapper that passes the typed status and accessible label to the native custom element.
- `styles.css`: visible focus, skip-link, responsive layout, and the semantic table's card-style reflow below 760 px.

## Database decisions

SQL Server is the deployed provider path and applies the checked-in EF Core migration. SQLite keeps local setup and provider-independent tests fast and creates its schema directly from the same EF Core model. The schema uses:

- a unique index on legacy procedure IDs to prevent duplicate migration records;
- indexes on procedure status/review date and action status/due date for dashboard queries;
- a foreign key from corrective actions to procedures;
- a concurrency token on `Revision` so stale browser updates receive HTTP 409;
- an append-only audit table for import and workflow events.

The SQL Server CI job starts SQL Server 2022, applies the real migration, inserts a procedure, clears the EF tracking state, and reads the row back through the SQL Server provider.

## Error handling

Known validation, conflict, and not-found failures are translated to RFC 7807 problem responses. Unexpected exceptions return a generic 500 response rather than exposing server details. The browser prevents an empty, oversized, or incorrectly named/typed CSV from being submitted, but this is only an early feedback layer. The API still rejects empty files and files over 1 MB, and the importer rejects missing columns, invalid statuses, invalid dates, and malformed quoted CSV values.

Successful REST responses do not cross into application state through TypeScript assertions alone. The frontend contract parsers validate object shape, strings, non-negative integer counts, positive revisions, known statuses, arrays, and import issues at runtime. Invalid server data becomes a controlled interface error.

## Frontend accessibility and responsive behavior

The register retains native table semantics at wide viewports, with a caption, scoped column headers, and labelled workflow controls. At widths below 760 px, CSS presents each row as a card and supplies visible labels from `data-label` attributes without changing the underlying table markup. A skip link targets the register, keyboard focus is visibly styled, errors receive programmatic focus, and polite/assertive live regions announce result counts, imports, successful workflow changes, and failures. Successful workflow confirmations also receive focus. Reduced-motion preferences disable smooth scrolling.

The loaded interface is scanned with axe-core during the Vitest suite. The `color-contrast` rule is excluded because jsdom cannot calculate rendered colors; contrast must therefore be checked in a real browser. These automated and structural checks reduce accessibility risk but do not constitute a WCAG conformance claim. Manual keyboard, screen-reader, zoom/reflow, and browser contrast checks remain part of a production readiness review.

## Automated quality gates

The frontend CI job uses Node.js 24 and runs reproducible installation, a high-severity npm audit, ESLint with zero warnings, TypeScript checking, 15 Vitest tests, a production build, and deterministic gzip bundle checks. JavaScript has a 55 KiB budget and CSS has a 5 KiB budget; the current verified output is 48.80 KiB and 2.03 KiB respectively. Backend CI runs 13 provider-independent tests, while a separate SQL Server service-container job runs the fourteenth test against the deployed-provider path.

## Trade-offs and limits

- Authentication, authorization, and role-based approval are outside this repository; a production workflow must bind approvals and tenant scope to verified identity claims and enforce them server-side.
- CSV is a migration seam, not a permanent integration contract. A production cutover should use signed export manifests and reconciliation reports.
- Audit rows are application-generated. Regulated environments may also require database-level immutability, retention policy, and trusted timestamps.
- Corrective actions are represented in the database and dashboard but do not yet have create/update HTTP routes.
- The revision token prevents lost updates. SQL Server `rowversion` could replace it if all supported database providers can use provider-specific concurrency behavior.
- Client-side validation, runtime response parsing, and accessible interaction patterns are defense-in-depth measures, not substitutes for authorization or evidence of full WCAG conformance.
