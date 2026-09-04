import type { Dashboard, ImportResult, ProcedureRecord, ProcedureStatus } from './types';
import {
  parseDashboard,
  parseImportResult,
  parseProcedure,
  parseProcedures,
  readProblemMessage,
} from './contract';

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';
export const maxImportBytes = 1_000_000;

async function readJson<T>(response: Response, parse: (value: unknown) => T): Promise<T> {
  const body: unknown = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(readProblemMessage(body) ?? (response.statusText || 'Request failed'));
  }
  return parse(body);
}

export async function loadDashboard(): Promise<Dashboard> {
  return readJson(
    await fetch(`${baseUrl}/api/dashboard`, { headers: { Accept: 'application/json' } }),
    parseDashboard,
  );
}

export async function loadProcedures(): Promise<ProcedureRecord[]> {
  return readJson(
    await fetch(`${baseUrl}/api/procedures`, { headers: { Accept: 'application/json' } }),
    parseProcedures,
  );
}

export async function changeProcedureStatus(
  procedure: ProcedureRecord,
  status: ProcedureStatus,
): Promise<ProcedureRecord> {
  return readJson(
    await fetch(`${baseUrl}/api/procedures/${procedure.id}/status`, {
      method: 'PATCH',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: JSON.stringify({ status, expectedRevision: procedure.revision }),
    }),
    parseProcedure,
  );
}

export function validateImportFile(file: File): string | null {
  if (file.size === 0) return 'Choose a non-empty CSV file.';
  if (file.size > maxImportBytes) return 'The CSV file must be smaller than 1 MB.';
  const hasCsvName = file.name.toLowerCase().endsWith('.csv');
  const hasCsvType =
    file.type === '' || file.type === 'text/csv' || file.type === 'application/vnd.ms-excel';
  if (!hasCsvName || !hasCsvType) return 'Choose a CSV file with a .csv extension.';
  return null;
}

export async function importProcedures(file: File): Promise<ImportResult> {
  const body = new FormData();
  body.append('file', file);
  return readJson(
    await fetch(`${baseUrl}/api/procedures/import`, {
      method: 'POST',
      headers: { Accept: 'application/json' },
      body,
    }),
    parseImportResult,
  );
}

