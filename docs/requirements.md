# Requirement-to-test traceability

| Requirement | Implementation | Verification |
| --- | --- | --- |
| Import controlled procedures from a legacy export | `LegacyProcedureImporter` validates the required CSV schema and upserts by legacy ID | Importer unit tests and HTTP multipart import test |
| Preserve commas and quotes in exported text | `CsvLineParser` handles quoted values and escaped quotes | Quoted-title import test and malformed-quote test |
| Reject files and records that cannot be migrated safely | Browser preflight checks extension/media type, empty content, and the 1 MB limit; the API independently enforces empty/size limits; importer validation checks IDs, required text, status, and ISO date format | Frontend import-validation tests, HTTP multipart test, invalid-status test, and missing-column test |
| Keep a trace of migrated and changed records | Every accepted import and workflow transition creates an `AuditEntry` | Import audit and status-change audit assertions |
| Prevent duplicate procedure records on repeat import | Unique database index plus importer upsert by `LegacyId` | Create/update importer test |
| Prevent stale browser updates from overwriting newer data | `Revision` is an EF concurrency token and is required in status updates | Service conflict test and HTTP 409 integration test |
| Preserve the result of a confirmed workflow write if a follow-up read fails | The updated procedure and focused success announcement are applied before refreshing the dashboard/register; refresh failure is reported separately | Successful workflow-action test and follow-up refresh-failure test |
| Allow only reviewed workflow transitions | Draft -> Active -> Archived transition policy in the workflow service | Three invalid-transition cases and one accepted-transition test |
| Show process health at a glance | Dashboard queries state counts, overdue reviews, and open corrective actions | Dashboard integration test and React summary rendering test |
| Reject malformed API data before it enters UI state | `contract.ts` parses unknown dashboard, procedure, and import-result payloads into validated application types | API contract acceptance and malformed-procedure rejection tests |
| Preserve useful server errors without exposing transport details | REST adapter reads RFC 7807 detail/title fields and converts failures to controlled interface errors | Stale workflow problem-detail and initial API failure tests |
| Support modern web use | React/TypeScript register with deferred search, status filtering, validated CSV upload, and revision-aware state actions | Vitest loading, filtering, workflow action/conflict, import, and failure-path tests; TypeScript check; production build |
| Provide a reusable framework-independent status element | React-free `workflow-status-badge.ts` defines the native custom element with Shadow DOM; `WorkflowStatusBadge.tsx` is a thin React wrapper | Web Component registration/rendering test |
| Provide an accessible interaction structure | Skip link, visible focus, focused alerts and workflow confirmations, polite live regions, labelled controls/actions, table caption, and scoped headers | Testing Library role/focus assertions and loaded-state axe-core scan |
| Report data availability without asserting unverified connectivity | The interface exposes loading, ready, and unavailable refresh states based on actual dashboard/register reads | Initial loading/success, initial API failure, and follow-up refresh-failure tests |
| Reflow the register for narrow screens | Media queries change the semantic table presentation to labelled row cards below 760 px and stack controls/metrics at smaller breakpoints | Production CSS build and documented manual browser review boundary |
| Keep frontend quality checks reproducible | Locked npm install followed by high-severity audit, ESLint with zero warnings, TypeScript checking, Vitest, and production build | GitHub Actions frontend job |
| Prevent unreviewed bundle growth | Deterministic gzip totals enforce 55 KiB JavaScript and 5 KiB CSS budgets | `npm run check:bundle`; current result: 48.80 KiB JS and 2.03 KiB CSS |
| Run on SQL Server | SQL Server provider, EF migration, compose environment, and CI service container | CI SQL Server migration and round-trip test |

The verified inventory is 15 frontend tests and 14 backend tests. The backend split is 13 provider-independent tests plus one SQL Server CI test. The frontend total includes component/workflow behavior, refresh-failure handling, CSV validation, runtime REST contracts, the Web Component, and an axe-core scan. The scan excludes `color-contrast` because jsdom cannot calculate rendered color values, and neither it nor this traceability matrix is a WCAG conformance claim. Authentication and authorization also remain outside the implemented requirements and must be added before production use.
