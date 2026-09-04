import {
  FormEvent,
  useDeferredValue,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import {
  changeProcedureStatus,
  importProcedures,
  loadDashboard,
  loadProcedures,
  validateImportFile,
} from './api';
import { WorkflowStatusBadge } from './components/WorkflowStatusBadge';
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
  const deferredQuery = useDeferredValue(query);
  const [statusFilter, setStatusFilter] = useState<'All' | ProcedureStatus>('All');
  const [busyIds, setBusyIds] = useState<Set<string>>(() => new Set());
  const [refreshState, setRefreshState] = useState<'checking' | 'ready' | 'unavailable'>(
    'checking',
  );
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [importing, setImporting] = useState(false);
  const [importMessage, setImportMessage] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const errorRef = useRef<HTMLDivElement>(null);
  const actionStatusRef = useRef<HTMLParagraphElement>(null);

  async function refresh() {
    try {
      const [nextDashboard, nextProcedures] = await Promise.all([
        loadDashboard(),
        loadProcedures(),
      ]);
      setDashboard(nextDashboard);
      setProcedures(nextProcedures);
      setRefreshState('ready');
    } catch (reason) {
      setRefreshState('unavailable');
      throw reason;
    }
  }

  useEffect(() => {
    refresh()
      .catch((reason: Error) => setError(reason.message))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (error) errorRef.current?.focus();
  }, [error]);

  useEffect(() => {
    if (actionMessage) actionStatusRef.current?.focus();
  }, [actionMessage]);

  const visibleProcedures = useMemo(() => {
    const normalizedQuery = deferredQuery.trim().toLowerCase();
    return procedures.filter((procedure) => {
      const matchesStatus = statusFilter === 'All' || procedure.status === statusFilter;
      const matchesQuery =
        normalizedQuery.length === 0 ||
        [procedure.legacyId, procedure.title, procedure.owner].some((value) =>
          value.toLowerCase().includes(normalizedQuery),
        );
      return matchesStatus && matchesQuery;
    });
  }, [procedures, deferredQuery, statusFilter]);

  async function advanceStatus(procedure: ProcedureRecord) {
    const requestedStatus = nextStatus(procedure.status);
    if (!requestedStatus) return;
    setBusyIds((current) => new Set(current).add(procedure.id));
    setError(null);
    setActionMessage(null);
    try {
      const updated = await changeProcedureStatus(procedure, requestedStatus);
      setProcedures((current) =>
        current.map((item) => (item.id === updated.id ? updated : item)),
      );
      setActionMessage(
        `${updated.legacyId} moved to ${updated.status}. Revision ${updated.revision}.`,
      );
      try {
        await refresh();
      } catch {
        setError('The status was saved, but the latest dashboard data could not be refreshed.');
      }
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Status update failed');
    } finally {
      setBusyIds((current) => {
        const next = new Set(current);
        next.delete(procedure.id);
        return next;
      });
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

    const validationMessage = validateImportFile(file);
    if (validationMessage) {
      setError(validationMessage);
      return;
    }

    setError(null);
    setImportMessage(null);
    setImporting(true);
    try {
      const result = await importProcedures(file);
      setImportMessage(
        `${result.created} created, ${result.updated} updated, ${result.rejected} rejected`,
      );
      await refresh();
      form.reset();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Import failed');
    } finally {
      setImporting(false);
    }
  }

  return (
    <>
      <a className="skip-link" href="#procedure-register">
        Skip to procedure register
      </a>
      <main>
        <header className="topbar">
          <div>
            <p className="eyebrow">Quality management workspace</p>
            <h1>Procedure control</h1>
            <p className="lede">
              Review migrated records, advance approved procedures, and trace every change.
            </p>
          </div>
          <div
            className={`system-chip system-chip-${refreshState}`}
            aria-label={`Data status: ${refreshState === 'ready' ? 'ready' : refreshState === 'checking' ? 'loading' : 'unavailable'}`}
          >
            <span aria-hidden="true" />
            {refreshState === 'ready'
              ? 'Data ready'
              : refreshState === 'checking'
                ? 'Loading data'
                : 'Data unavailable'}
          </div>
        </header>

        {error && (
          <div className="alert" role="alert" tabIndex={-1} ref={errorRef}>
            {error}
          </div>
        )}

        <section className="metrics" aria-label="Workflow summary">
          <Metric label="All procedures" value={dashboard.totalProcedures} />
          <Metric label="Active" value={dashboard.activeProcedures} tone="green" />
          <Metric label="Draft" value={dashboard.draftProcedures} tone="blue" />
          <Metric label="Overdue reviews" value={dashboard.overdueReviews} tone="amber" />
          <Metric label="Open actions" value={dashboard.openCorrectiveActions} tone="red" />
        </section>

        <section
          className="workspace"
          id="procedure-register"
          aria-labelledby="procedure-register-heading"
          aria-busy={loading || importing}
        >
          <div className="section-heading">
            <div>
              <p className="eyebrow">Controlled documents</p>
              <h2 id="procedure-register-heading">Procedure register</h2>
            </div>
            <form className="import-form" onSubmit={handleImport}>
              <label className="file-picker">
                <span>Legacy CSV</span>
                <input
                  name="legacyFile"
                  type="file"
                  accept=".csv,text/csv"
                  aria-describedby="import-help"
                />
              </label>
              <span className="sr-only" id="import-help">
                CSV files only, up to 1 megabyte.
              </span>
              <button type="submit" disabled={importing}>
                {importing ? 'Importing...' : 'Import records'}
              </button>
            </form>
          </div>
          {importMessage && (
            <p className="import-result" role="status">
              Import complete: {importMessage}
            </p>
          )}
          {actionMessage && (
            <p className="action-result" role="status" tabIndex={-1} ref={actionStatusRef}>
              {actionMessage}
            </p>
          )}

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
                onChange={(event) =>
                  setStatusFilter(event.target.value as 'All' | ProcedureStatus)
                }
              >
                <option>All</option>
                <option>Draft</option>
                <option>Active</option>
                <option>Archived</option>
              </select>
            </label>
          </div>

          <p className="sr-only" aria-live="polite">
            {loading
              ? 'Loading procedures.'
              : `${visibleProcedures.length} procedure${visibleProcedures.length === 1 ? '' : 's'} shown.`}
          </p>

          {loading ? (
            <p className="empty-state">Loading procedure register...</p>
          ) : (
            <div className="table-wrap">
              <table>
                <caption className="sr-only">
                  Controlled procedures with workflow status and available actions
                </caption>
                <thead>
                  <tr>
                    <th scope="col">Legacy ID</th>
                    <th scope="col">Procedure</th>
                    <th scope="col">Owner</th>
                    <th scope="col">Version</th>
                    <th scope="col">Review due</th>
                    <th scope="col">Status</th>
                    <th scope="col">
                      <span className="sr-only">Workflow action</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {visibleProcedures.map((procedure) => {
                    const requestedStatus = nextStatus(procedure.status);
                    return (
                      <tr key={procedure.id}>
                        <td className="mono" data-label="Legacy ID">
                          {procedure.legacyId}
                        </td>
                        <td data-label="Procedure">
                          <strong>{procedure.title}</strong>
                          <small>Revision {procedure.revision}</small>
                        </td>
                        <td data-label="Owner">{procedure.owner}</td>
                        <td data-label="Version">{procedure.version}</td>
                        <td data-label="Review due">{procedure.reviewDueOn}</td>
                        <td data-label="Status">
                          <WorkflowStatusBadge status={procedure.status} />
                        </td>
                        <td className="action-cell" data-label="Action">
                          {requestedStatus && (
                            <button
                              type="button"
                              className="text-button"
                              disabled={busyIds.has(procedure.id)}
                              aria-label={`Move ${procedure.legacyId} to ${requestedStatus}`}
                              onClick={() => advanceStatus(procedure)}
                            >
                              {busyIds.has(procedure.id)
                                ? 'Saving...'
                                : `Move to ${requestedStatus}`}
                            </button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
              {visibleProcedures.length === 0 && (
                <p className="empty-state">No procedures match these filters.</p>
              )}
            </div>
          )}
        </section>
      </main>
    </>
  );
}

function Metric({
  label,
  value,
  tone = 'neutral',
}: {
  label: string;
  value: number;
  tone?: string;
}) {
  return (
    <article className={`metric metric-${tone}`} aria-label={`${label}: ${value}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}
