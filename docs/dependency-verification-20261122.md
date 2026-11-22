# Dependency verification - 22 November 2026

The existing frontend lockfile reported nine vulnerable package entries (two high,
seven moderate) in `npm audit`. These are dependency findings, not nine verified
exploitable application paths. Updated Vite from 6.4.3 to 6.4.6 and refreshed
compatible transitive dependencies without bypassing peer checks or using forced
major upgrades. The CI audit threshold is now `low`.

Verification used .NET SDK 8.0.423, Node 24.22.3 and npm 10.8.2:

- Clean `npm ci` installation: passed.
- `npm audit --audit-level=low`: zero reported vulnerabilities.
- ESLint and TypeScript checks: passed.
- Frontend tests: 17 passed.
- Production build and gzip budgets: passed; 48.81 KiB JavaScript and 2.03 KiB CSS.
- Provider-independent Release backend tests: 22 passed.

The SQL Server migration/round-trip test runs separately in GitHub Actions against
SQL Server 2022. It is excluded from the local provider-independent count; a test
invoked without its connection variable is not treated as real-provider evidence.
The AI tests use local rules and controlled HTTP responses, not a live paid model.

Dependency audits are time-sensitive. Repeat the documented checks before relying
on this record for a later revision. No production deployment or incident outcome
is claimed by this maintenance change.
