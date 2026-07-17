import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import App from './App';
import * as api from './api';
import type { Dashboard, ProcedureRecord } from './types';

vi.mock('./api');

const dashboard: Dashboard = {
  totalProcedures: 2,
  draftProcedures: 1,
  activeProcedures: 1,
  archivedProcedures: 0,
  overdueReviews: 1,
  openCorrectiveActions: 1,
};

const procedures: ProcedureRecord[] = [
  {
    id: 'one',
    legacyId: 'PROC-001',
    title: 'Document control',
    owner: 'Quality',
    version: '1.0',
    status: 'Draft',
    reviewDueOn: '2026-10-01',
    revision: 1,
    updatedAt: '2026-07-17T10:00:00Z',
  },
  {
    id: 'two',
    legacyId: 'PROC-002',
    title: 'Supplier review',
    owner: 'Purchasing',
    version: '2.0',
    status: 'Active',
    reviewDueOn: '2026-08-01',
    revision: 3,
    updatedAt: '2026-07-17T10:00:00Z',
  },
];

describe('procedure register', () => {
  beforeEach(() => {
    vi.mocked(api.loadDashboard).mockResolvedValue(dashboard);
    vi.mocked(api.loadProcedures).mockResolvedValue(procedures);
    vi.mocked(api.changeProcedureStatus).mockResolvedValue({
      ...procedures[0],
      status: 'Active',
      revision: 2,
    });
  });

  it('loads summary counts and filters procedures by owner', async () => {
    render(<App />);

    expect(await screen.findByText('Document control')).toBeInTheDocument();
    expect(screen.getByText('Supplier review')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'Purchasing' } });

    expect(screen.queryByText('Document control')).not.toBeInTheDocument();
    expect(screen.getByText('Supplier review')).toBeInTheDocument();
    expect(screen.getByText('2', { selector: '.metric strong' })).toBeInTheDocument();
  });

  it('sends the current revision when advancing a workflow state', async () => {
    render(<App />);
    await screen.findByText('Document control');

    fireEvent.click(screen.getByRole('button', { name: 'Move to Active' }));

    await waitFor(() => expect(api.changeProcedureStatus).toHaveBeenCalledWith(
      procedures[0],
      'Active',
    ));
    expect(await screen.findByText('Revision 2')).toBeInTheDocument();
  });
});

