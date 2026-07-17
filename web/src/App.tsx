import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  changeProcedureStatus,
  importProcedures,
  loadDashboard,
  loadProcedures,
} from './api';
import type { Dashboard, ProcedureRecord, ProcedureStatus } from './types';

const emptyDashboard: Dashboard = {
  totalProcedures: 0,
  draftProcedures: 0,
  activeProcedures: 0,
  archivedProcedures: 0,
  overdueReviews: 0,
  openCorrectiveActions: 0,
};

function nextStatus(status: ProcedureStatus): ProcedureStatus | null {
  if (status === 'Draft') return 'Active';
  if (status === 'Active') return 'Archived';
  return null;
}

export default function App() {
  const [dashboard, setDashboard] = useState<Dashboard>(emptyDashboard);
  const [procedures, setProcedures] = useState<ProcedureRecord[]>([]);
  const [query, setQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<'All' | ProcedureStatus>('All');
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [importMessage, setImportMessage] = useState<string | null>(null);

  async function refresh() {
    const [nextDashboard, nextProcedures] = await Promise.all([
      loadDashboard(),
      loadProcedures(),
    ]);
    setDashboard(nextDashboard);
    setProcedures(nextProcedures);
  }

  useEffect(() => {
    refresh()
      .catch((reason: Error) => setError(reason.message))
      .finally(() => setLoading(false));
  }, []);

  const visibleProcedures = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase();
    return procedures.filter((procedure) => {
      const matchesStatus = statusFilter === 'All' || procedure.status === statusFilter;
      const matchesQuery =
        normalizedQuery.length === 0 ||
        [procedure.legacyId, procedure.title, procedure.owner]
          .some((value) => value.toLowerCase().includes(normalizedQuery));
      return matchesStatus && matchesQuery;
    });
  }, [procedures, query, statusFilter]);

  async function advanceStatus(procedure: ProcedureRecord) {
    const requestedStatus = nextStatus(procedure.status);
    if (!requestedStatus) return;
    setBusyId(procedure.id);
    setError(null);
    try {
      const updated = await changeProcedureStatus(procedure, requestedStatus);
      setProcedures((current) => current.map((item) => item.id === updated.id ? updated : item));
      setDashboard(await loadDashboard());
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Status update failed');
    } finally {
      setBusyId(null);
    }
  }

  async function handleImport(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const input = form.elements.namedItem('legacyFile') as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      setError('Choose a CSV file before importing.');
      return;
    }

    setError(null);
    try {
      const result = await importProcedures(file);
      setImportMessage(
        `${result.created} created, ${result.updated} updated, ${result.rejected} rejected`,
      );
      await refresh();
      form.reset();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Import failed');
    }
  }

  return (
    <main>
      <header className="topbar">
        <div>
          <p className="eyebrow">Quality management workspace</p>
          <h1>Procedure control</h1>
          <p className="lede">Review migrated records, advance approved procedures, and trace every change.</p>
        </div>
        <div className="system-chip"><span /> Database connected</div>
      </header>

      {error && <div className="alert" role="alert">{error}</div>}

      <section className="metrics" aria-label="Workflow summary">
        <Metric label="All procedures" value={dashboard.totalProcedures} />
        <Metric label="Active" value={dashboard.activeProcedures} tone="green" />
        <Metric label="Draft" value={dashboard.draftProcedures} tone="blue" />
        <Metric label="Overdue reviews" value={dashboard.overdueReviews} tone="amber" />
        <Metric label="Open actions" value={dashboard.openCorrectiveActions} tone="red" />
      </section>

      <section className="workspace">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Controlled documents</p>
            <h2>Procedure register</h2>
          </div>
          <form className="import-form" onSubmit={handleImport}>
            <label className="file-picker">
              <span>Legacy CSV</span>
              <input name="legacyFile" type="file" accept=".csv,text/csv" />
            </label>
            <button type="submit">Import records</button>
          </form>
        </div>
        {importMessage && <p className="import-result" role="status">Import complete: {importMessage}</p>}

        <div className="filters">
          <label>
            <span>Search</span>
            <input
              type="search"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="ID, title, or owner"
            />
          </label>
          <label>
            <span>Status</span>
            <select
              value={statusFilter}
              onChange={(event) => setStatusFilter(event.target.value as 'All' | ProcedureStatus)}
            >
              <option>All</option>
              <option>Draft</option>
              <option>Active</option>
              <option>Archived</option>
            </select>
          </label>
        </div>

        {loading ? (
          <p className="empty-state">Loading procedure register...</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Legacy ID</th>
                  <th>Procedure</th>
                  <th>Owner</th>
                  <th>Version</th>
                  <th>Review due</th>
                  <th>Status</th>
                  <th><span className="sr-only">Workflow action</span></th>
                </tr>
              </thead>
              <tbody>
                {visibleProcedures.map((procedure) => {
                  const requestedStatus = nextStatus(procedure.status);
                  return (
                    <tr key={procedure.id}>
                      <td className="mono">{procedure.legacyId}</td>
                      <td><strong>{procedure.title}</strong><small>Revision {procedure.revision}</small></td>
                      <td>{procedure.owner}</td>
                      <td>{procedure.version}</td>
                      <td>{procedure.reviewDueOn}</td>
                      <td><span className={`status status-${procedure.status.toLowerCase()}`}>{procedure.status}</span></td>
                      <td className="action-cell">
                        {requestedStatus && (
                          <button
                            className="text-button"
                            disabled={busyId === procedure.id}
                            onClick={() => advanceStatus(procedure)}
                          >
                            {busyId === procedure.id ? 'Saving...' : `Move to ${requestedStatus}`}
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            {visibleProcedures.length === 0 && <p className="empty-state">No procedures match these filters.</p>}
          </div>
        )}
      </section>
    </main>
  );
}

function Metric({ label, value, tone = 'neutral' }: { label: string; value: number; tone?: string }) {
  return (
    <article className={`metric metric-${tone}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

