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
- Runs with SQLite for local development and SQL Server 2022 for the deployed-provider path.

## Technology

- C# / .NET 8 / ASP.NET Core Minimal API
- Entity Framework Core 8 with SQL Server and SQLite providers
- React 18, TypeScript, Vite, HTML, and CSS
- xUnit, ASP.NET Core integration testing, Vitest, and GitHub Actions

## Architecture

```text
Legacy CSV -> validation/upsert service -> ASP.NET Core API -> EF Core -> SQL Server
                                              ^
                                              |
                                       React web client
```

The service layer owns import rules and workflow transitions. EF Core owns persistence, indexes, migration history, and concurrency checks. The web client sends the revision it last read so the API can reject stale writes. See [architecture](docs/architecture.md), [traceability](docs/requirements.md), and the [migration plan](docs/migration-plan.md).

## Run locally with SQLite

Prerequisites: .NET 8 SDK and Node.js 18 or later.

```powershell
dotnet tool restore
dotnet restore QualityWorkflow.sln
dotnet run --project src/QualityWorkflow.Api
```

The API starts at `http://localhost:5080`, creates the SQLite schema from the EF Core model, writes `quality-workflow.db`, and loads three synthetic records.

In a second terminal:

```powershell
cd web
npm install
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

## Verify

```powershell
dotnet test QualityWorkflow.sln --configuration Release
cd web
npm audit --audit-level=high
npm test
npm run build
```

The verified test inventory is 14 backend tests and 2 React tests. Backend checks cover migration imports, malformed data, workflow transitions, stale revisions, SQLite initialization, HTTP behavior, dashboard aggregation, and a SQL Server-specific migration/round-trip path. CI runs provider-independent tests, a high-severity npm dependency audit, the frontend build/tests, and the SQL Server test as separate jobs.

## Limitations

- Authentication, authorization, tenant isolation, notifications, and file attachments are outside this repository.
- CSV is treated as a controlled cutover format rather than a permanent synchronization channel.
- Corrective actions are included in the data model and dashboard counts but do not yet expose write endpoints.
- Production operation would add secrets management, database backups, immutable audit retention, monitoring, and a rehearsed rollback window.
