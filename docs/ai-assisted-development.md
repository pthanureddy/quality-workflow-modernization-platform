# AI-assisted development record

## 6 October 2026: confirmed import followed by a failed refresh

This independent portfolio change was developed with Codex assistance. It was found during source review, not reported by a real customer. No production or commercial deployment is implied.

### Problem and acceptance criteria

The import API can save records successfully while the following dashboard/register read fails. The browser previously passed both operations through one catch block, so it displayed the refresh exception next to the confirmed import without explaining which operation failed. The form was reset only after a successful refresh.

The interface must keep the confirmed import counts, reset the completed upload form, report the unavailable dashboard separately, and retain the last loaded register. If the import API itself rejects the upload, the interface must show its error without announcing a saved import or starting a follow-up refresh.

### Implementation and checks

- Added two behavioral React regressions covering the confirmed-write/read-failure case and import rejection. The first failed on the original implementation with `Refresh failed` instead of the required saved-import explanation; the rejection check already passed.
- Kept the API/importer contract unchanged. Reset the form immediately after the accepted import and handle its follow-up refresh in a separate catch block.
- Reviewed the generated change against the existing workflow state handling, authoritative API validation, and runtime response contracts. Generated output was not treated as proof of correctness.
- Ran ESLint, TypeScript checking, all 17 frontend tests, the production build and deterministic bundle gates. Ran the 22 provider-independent .NET tests, formatting and backend build.
- Patched the transitive `source-map-js` dependency to its non-vulnerable compatible release after the current npm audit found a high-severity advisory. The full audit then reported zero known vulnerabilities at verification.
- Exercised the actual local React/ASP.NET Core/SQLite stack in a browser: loaded dashboard counts, filtered the register, advanced `PROC-014` from Draft to Active, and confirmed revision 2 persisted after reload. The refresh-failure case is verified through failure injection in the React suite.

The SQL Server provider, container image and Azure Bicep blueprint are checked by CI. These checks establish build and integration behavior, not a live cloud deployment. AI reviewer output is still a suggestion requiring human review; this coding-agent-assisted change does not establish paid-model accuracy, production autonomy, or a quantified development speedup.

## Audit endpoint provider issue

The local smoke check also found that `GET /api/audit` returned HTTP 500 when EF Core tried to order SQLite rows by `DateTimeOffset`. The limitation is documented by [Microsoft](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations). A new HTTP regression reproduced the failure before the fix.

The endpoint now orders the latest 100 persisted appends by the numeric audit ID while preserving each event timestamp. It retains a bounded database query and needs no schema migration. The test inserts 105 entries with deliberately opposing timestamp order and checks HTTP success, the 100-row limit, descending append IDs, and unchanged timestamps. The contract is append history rather than event-time chronology.
