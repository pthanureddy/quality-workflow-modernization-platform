import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  changeProcedureStatus,
  loadDashboard,
  loadProcedures,
  validateImportFile,
} from './api';
import type { ProcedureRecord } from './types';

const procedure: ProcedureRecord = {
  id: '28c67179-7069-48b8-aab0-2ed66e84e68d',
  legacyId: 'PROC-001',
  title: 'Document control',
  owner: 'Quality',
  version: '1.0',
  status: 'Draft',
  reviewDueOn: '2026-10-01',
  revision: 1,
  updatedAt: '2026-07-17T10:00:00Z',
};

function jsonResponse(value: unknown, status = 200, statusText = 'OK') {
  return new Response(JSON.stringify(value), {
    status,
    statusText,
    headers: { 'Content-Type': 'application/json' },
  });
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('typed API boundary', () => {
  it('accepts a valid dashboard contract', async () => {
    const value = {
      totalProcedures: 2,
      draftProcedures: 1,
      activeProcedures: 1,
      archivedProcedures: 0,
      overdueReviews: 1,
      openCorrectiveActions: 1,
    };
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(value)));

    await expect(loadDashboard()).resolves.toEqual(value);
  });

  it('rejects malformed procedure data instead of trusting a type assertion', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse([{ ...procedure, revision: 0 }])),
    );

    await expect(loadProcedures()).rejects.toThrow('revision must be a positive integer');
  });

  it('preserves a problem-detail message for a stale workflow update', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(
          jsonResponse({ detail: 'The procedure was changed by another user.' }, 409, 'Conflict'),
        ),
    );

    await expect(changeProcedureStatus(procedure, 'Active')).rejects.toThrow(
      'The procedure was changed by another user.',
    );
  });

  it('validates CSV extension, media type, size, and empty content before upload', () => {
    const valid = new File(['legacy_id,title'], 'procedures.csv', { type: 'text/csv' });
    const wrongType = new File(['text'], 'procedures.txt', { type: 'text/plain' });
    const empty = new File([], 'procedures.csv', { type: 'text/csv' });
    const oversized = new File(['data'], 'procedures.csv', { type: 'text/csv' });
    Object.defineProperty(oversized, 'size', { value: 1_000_001 });

    expect(validateImportFile(valid)).toBeNull();
    expect(validateImportFile(wrongType)).toContain('.csv extension');
    expect(validateImportFile(empty)).toContain('non-empty');
    expect(validateImportFile(oversized)).toContain('smaller than 1 MB');
  });
});
