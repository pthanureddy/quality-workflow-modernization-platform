import type { Dashboard, ImportResult, ProcedureRecord, ProcedureStatus } from './types';

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const problem = await response.json().catch(() => ({ title: response.statusText }));
    throw new Error(problem.detail ?? problem.title ?? 'Request failed');
  }

  return response.json() as Promise<T>;
}

export async function loadDashboard(): Promise<Dashboard> {
  return readJson(await fetch(`${baseUrl}/api/dashboard`));
}

export async function loadProcedures(): Promise<ProcedureRecord[]> {
  return readJson(await fetch(`${baseUrl}/api/procedures`));
}

export async function changeProcedureStatus(
  procedure: ProcedureRecord,
  status: ProcedureStatus,
): Promise<ProcedureRecord> {
  return readJson(
    await fetch(`${baseUrl}/api/procedures/${procedure.id}/status`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status, expectedRevision: procedure.revision }),
    }),
  );
}

export async function importProcedures(file: File): Promise<ImportResult> {
  const body = new FormData();
  body.append('file', file);
  return readJson(
    await fetch(`${baseUrl}/api/procedures/import`, {
      method: 'POST',
      body,
    }),
  );
}

