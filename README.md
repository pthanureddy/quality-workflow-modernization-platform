# Quality Workflow Modernization Platform

An independent full-stack portfolio implementation of one established-product modernization problem: migrating controlled procedure records from a legacy export into a web-based workflow backed by a relational database.

The repository is not connected to a commercial quality-management product and contains only synthetic sample data.

## What the system does

- Imports UTF-8 CSV procedure exports through a validated migration boundary.
- Creates or updates records idempotently by legacy ID and reports rejected rows without inventing data.
- Stores procedures, corrective actions, and audit entries through EF Core 8.
- Enforces Draft -> Active -> Archived transitions with revision-based optimistic concurrency.
- Returns HTTP 409 when a browser attempts to overwrite a newer revision.
- Shows procedure status, overdue review, and open-action counts in a React/TypeScript dashboard.
- Parses unknown API JSON at runtime before it can enter typed React state.
- Validates CSV file name, media type, size, and empty content in the browser while retaining authoritative API and importer validation.
- Renders workflow states through a reusable native `workflow-status-badge` Web Component.
- Supports keyboard navigation, focused status announcements for successful workflow changes, a semantic data table, and a responsive card reflow for narrow screens.
- Reports data refresh state as loading, ready, or unavailable rather than presenting a static connectivity claim.
- Runs with SQLite for local development and SQL Server 2022 for the deployed-provider path.

## Technology

- C# / .NET 8 / ASP.NET Core Minimal API
- Entity Framework Core 8 with SQL Server and SQLite providers
- React 18, TypeScript, Vite, a native Web Component, HTML, and CSS
- xUnit, ASP.NET Core integration testing, Vitest, Testing Library, axe-core, ESLint, and GitHub Actions

## Architecture

```text
Legacy CSV -> validation/upsert service -> ASP.NET Core API -> EF Core -> SQL Server
                                              ^
                                              |
                                       React web client
```

The service layer owns import rules and workflow transitions. EF Core owns persistence, indexes, migration history, and concurrency checks. The web client sends the revision it last read so the API can reject stale writes. Its REST adapter treats response bodies as unknown data and converts them to application types only after explicit contract checks. See [architecture](docs/architecture.md), [traceability](docs/requirements.md), and the [migration plan](docs/migration-plan.md).

## Run locally with SQLite

Prerequisites: .NET 8 SDK and Node.js 24 (the version used in CI).

```powershell
dotnet tool restore
dotnet restore QualityWorkflow.sln
dotnet run --project src/QualityWorkflow.Api
```

The API starts at `http://localhost:5080`, creates the SQLite schema from the EF Core model, writes `quality-workflow.db`, and loads three synthetic records.

In a second terminal:

```powershell
cd web
npm ci
npm run dev
```

Open `http://localhost:5173`.

## Run with SQL Server 2022

Start the database using a local development password that meets SQL Server complexity rules:

```powershell
$env:MSSQL_SA_PASSWORD="Choose-A-Local-Password!2026"
docker compose up -d sqlserver
```

Configure the API without committing credentials:

```powershell
$env:DatabaseProvider="SqlServer"
$env:ConnectionStrings__QualityWorkflow="Server=localhost,1433;Database=QualityWorkflow;User Id=sa;Password=$env:MSSQL_SA_PASSWORD;TrustServerCertificate=true;Encrypt=false"
dotnet run --project src/QualityWorkflow.Api
```

The API applies `InitialCreate` before serving requests. GitHub Actions independently starts SQL Server 2022, applies the same migration, and performs a database round trip.

## Import a legacy export

Use [samples/legacy-procedures.csv](samples/legacy-procedures.csv) in the web interface, or call the API:

```powershell
curl.exe -X POST http://localhost:5080/api/procedures/import `
  -F "file=@samples/legacy-procedures.csv;type=text/csv"
```

Required columns are `legacy_id`, `title`, `owner`, `version`, `status`, and `review_due_on`. Dates use `yyyy-MM-dd`. The response separates created, updated, and rejected rows and includes a reason for each rejection.

The browser rejects an empty file, a file over 1 MB, or a non-CSV name/media type before making a request. This is an early usability boundary, not a security boundary: the API independently enforces non-empty and 1 MB limits, and the importer enforces the required schema and validates every row.

## Verify

```powershell
dotnet test QualityWorkflow.sln --configuration Release
cd web
npm audit --audit-level=high
npm run lint
npm run typecheck
npm test
npm run build
npm run check:bundle
```

The verified inventory is 15 frontend tests and 14 backend tests. The backend total consists of 13 provider-independent tests plus one SQL Server migration/round-trip test. Frontend checks cover loading and refresh state, filtering, workflow actions and conflicts, retention of a confirmed change when its follow-up refresh fails, import validation/results, API failure handling, runtime response contracts, the Web Component, and an axe-core scan. The axe test disables `color-contrast` because jsdom does not calculate the rendered color information that rule requires; it is one automated check, not a WCAG conformance claim.

The production build is also checked against deterministic gzip budgets: 55 KiB for all JavaScript and 5 KiB for all CSS. The current verified output is 48.80 KiB of JavaScript and 2.03 KiB of CSS. CI runs the dependency audit, ESLint, TypeScript checking, frontend tests, production build, bundle budgets, provider-independent backend tests, and the SQL Server path.

## Limitations

- Authentication, authorization, tenant isolation, notifications, and file attachments are outside this repository; the interface must not be treated as production-secure until identity and permission checks exist at the API boundary.
- CSV is treated as a controlled cutover format rather than a permanent synchronization channel.
- Corrective actions are included in the data model and dashboard counts but do not yet expose write endpoints.
- Production operation would add secrets management, database backups, immutable audit retention, monitoring, and a rehearsed rollback window.
- The accessibility work covers semantic structure, focus handling, live regions, responsive reflow, and an automated axe check. It does not assert WCAG conformance; manual keyboard, screen-reader, zoom/reflow, and browser-based contrast testing remain necessary.
