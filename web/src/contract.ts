import type { Dashboard, ImportResult, ProcedureRecord, ProcedureStatus } from './types';

const procedureStatuses = new Set<ProcedureStatus>(['Draft', 'Active', 'Archived']);

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function requireObject(value: unknown, context: string): Record<string, unknown> {
  if (!isObject(value)) {
    throw new Error(`Invalid ${context} response.`);
  }
  return value;
}

function requireString(value: unknown, field: string): string {
  if (typeof value !== 'string') {
    throw new Error(`Invalid API response: ${field} must be text.`);
  }
  return value;
}

function requireCount(value: unknown, field: string): number {
  if (typeof value !== 'number' || !Number.isInteger(value) || value < 0) {
    throw new Error(`Invalid API response: ${field} must be a non-negative integer.`);
  }
  return value;
}

function requireRevision(value: unknown): number {
  if (typeof value !== 'number' || !Number.isInteger(value) || value < 1) {
    throw new Error('Invalid API response: revision must be a positive integer.');
  }
  return value;
}

function requireStatus(value: unknown): ProcedureStatus {
  if (typeof value !== 'string' || !procedureStatuses.has(value as ProcedureStatus)) {
    throw new Error('Invalid API response: unknown procedure status.');
  }
  return value as ProcedureStatus;
}

export function parseDashboard(value: unknown): Dashboard {
  const record = requireObject(value, 'dashboard');
  return {
    totalProcedures: requireCount(record.totalProcedures, 'totalProcedures'),
    draftProcedures: requireCount(record.draftProcedures, 'draftProcedures'),
    activeProcedures: requireCount(record.activeProcedures, 'activeProcedures'),
    archivedProcedures: requireCount(record.archivedProcedures, 'archivedProcedures'),
    overdueReviews: requireCount(record.overdueReviews, 'overdueReviews'),
    openCorrectiveActions: requireCount(record.openCorrectiveActions, 'openCorrectiveActions'),
  };
}

export function parseProcedure(value: unknown): ProcedureRecord {
  const record = requireObject(value, 'procedure');
  return {
    id: requireString(record.id, 'id'),
    legacyId: requireString(record.legacyId, 'legacyId'),
    title: requireString(record.title, 'title'),
    owner: requireString(record.owner, 'owner'),
    version: requireString(record.version, 'version'),
    status: requireStatus(record.status),
    reviewDueOn: requireString(record.reviewDueOn, 'reviewDueOn'),
    revision: requireRevision(record.revision),
    updatedAt: requireString(record.updatedAt, 'updatedAt'),
  };
}

export function parseProcedures(value: unknown): ProcedureRecord[] {
  if (!Array.isArray(value)) {
    throw new Error('Invalid procedures response.');
  }
  return value.map(parseProcedure);
}

export function parseImportResult(value: unknown): ImportResult {
  const record = requireObject(value, 'import');
  if (!Array.isArray(record.issues)) {
    throw new Error('Invalid API response: issues must be a list.');
  }
  return {
    created: requireCount(record.created, 'created'),
    updated: requireCount(record.updated, 'updated'),
    rejected: requireCount(record.rejected, 'rejected'),
    issues: record.issues.map((value) => {
      const issue = requireObject(value, 'import issue');
      return {
        rowNumber: requireCount(issue.rowNumber, 'rowNumber'),
        message: requireString(issue.message, 'message'),
      };
    }),
  };
}

export function readProblemMessage(value: unknown): string | null {
  if (!isObject(value)) return null;
  if (typeof value.detail === 'string' && value.detail.trim()) return value.detail;
  if (typeof value.title === 'string' && value.title.trim()) return value.title;
  return null;
}
