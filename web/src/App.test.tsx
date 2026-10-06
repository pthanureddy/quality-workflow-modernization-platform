import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import axe from 'axe-core';
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
    vi.resetAllMocks();
    vi.mocked(api.loadDashboard).mockResolvedValue(dashboard);
    vi.mocked(api.loadProcedures).mockResolvedValue(procedures);
    vi.mocked(api.validateImportFile).mockReturnValue(null);
    vi.mocked(api.importProcedures).mockResolvedValue({
      created: 1,
      updated: 1,
      rejected: 0,
      issues: [],
    });
    vi.mocked(api.changeProcedureStatus).mockResolvedValue({
      ...procedures[0],
      status: 'Active',
      revision: 2,
    });
  });

  it('shows a loading state before rendering the summary and procedures', async () => {
    render(<App />);

    expect(screen.getByText('Loading procedure register...')).toBeInTheDocument();
    expect(screen.getByText('Loading data')).toBeInTheDocument();
    expect(await screen.findByText('Document control')).toBeInTheDocument();
    expect(screen.getByLabelText('All procedures: 2')).toBeInTheDocument();
    expect(screen.getByText('Data ready')).toBeInTheDocument();
  });

  it('filters procedures by search term and workflow status', async () => {
    render(<App />);
    await screen.findByText('Document control');

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'Purchasing' } });
    await waitFor(() => expect(screen.queryByText('Document control')).not.toBeInTheDocument());
    expect(screen.getByText('Supplier review')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'Draft' } });
    expect(await screen.findByText('No procedures match these filters.')).toBeInTheDocument();
  });

  it('sends the current revision when advancing a workflow state', async () => {
    const updatedProcedure = { ...procedures[0], status: 'Active' as const, revision: 2 };
    vi.mocked(api.loadProcedures)
      .mockResolvedValueOnce(procedures)
      .mockResolvedValueOnce([updatedProcedure, procedures[1]]);
    let resolveStatus!: (value: ProcedureRecord) => void;
    vi.mocked(api.changeProcedureStatus).mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveStatus = resolve;
        }),
    );
    render(<App />);
    await screen.findByText('Document control');

    const action = screen.getByRole('button', { name: 'Move PROC-001 to Active' });
    fireEvent.click(action);

    await waitFor(() =>
      expect(api.changeProcedureStatus).toHaveBeenCalledWith(procedures[0], 'Active'),
    );
    expect(action).toBeDisabled();
    await act(async () => {
      resolveStatus(updatedProcedure);
    });
    expect(await screen.findByText('Revision 2')).toBeInTheDocument();
    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('PROC-001 moved to Active. Revision 2.');
    expect(status).toHaveFocus();
  });

  it('surfaces and focuses a workflow conflict returned by the API', async () => {
    vi.mocked(api.changeProcedureStatus).mockRejectedValue(
      new Error('The procedure was changed by another user.'),
    );
    render(<App />);
    await screen.findByText('Document control');

    fireEvent.click(screen.getByRole('button', { name: 'Move PROC-001 to Active' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('The procedure was changed by another user.');
    expect(alert).toHaveFocus();
  });

  it('keeps a confirmed status change when the follow-up refresh fails', async () => {
    vi.mocked(api.loadProcedures)
      .mockResolvedValueOnce(procedures)
      .mockRejectedValueOnce(new Error('Refresh failed'));
    render(<App />);
    await screen.findByText('Document control');

    fireEvent.click(screen.getByRole('button', { name: 'Move PROC-001 to Active' }));

    expect(await screen.findByText('Revision 2')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('PROC-001 moved to Active');
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The status was saved, but the latest dashboard data could not be refreshed.',
    );
    expect(screen.getByText('Data unavailable')).toBeInTheDocument();
  });

  it('requires a file before submitting a legacy import', async () => {
    render(<App />);
    await screen.findByText('Document control');

    fireEvent.click(screen.getByRole('button', { name: 'Import records' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Choose a CSV file before importing.',
    );
    expect(api.importProcedures).not.toHaveBeenCalled();
  });

  it('stops an invalid file at the browser validation boundary', async () => {
    vi.mocked(api.validateImportFile).mockReturnValue('Choose a CSV file with a .csv extension.');
    render(<App />);
    await screen.findByText('Document control');
    const file = new File(['not,csv'], 'procedures.txt', { type: 'text/plain' });

    fireEvent.change(screen.getByLabelText('Legacy CSV'), { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: 'Import records' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Choose a CSV file');
    expect(api.importProcedures).not.toHaveBeenCalled();
  });

  it('imports a validated CSV and announces the result', async () => {
    render(<App />);
    await screen.findByText('Document control');
    const file = new File(['legacy_id,title'], 'procedures.csv', { type: 'text/csv' });

    fireEvent.change(screen.getByLabelText('Legacy CSV'), { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: 'Import records' }));

    expect(await screen.findByRole('status')).toHaveTextContent(
      'Import complete: 1 created, 1 updated, 0 rejected',
    );
    expect(api.importProcedures).toHaveBeenCalledWith(file);
    expect(api.loadProcedures).toHaveBeenCalledTimes(2);
  });

  it('reports an initial API failure without exposing implementation details', async () => {
    vi.mocked(api.loadDashboard).mockRejectedValue(new Error('Service unavailable'));
    render(<App />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Service unavailable');
    expect(alert).toHaveFocus();
    expect(screen.getByText('Data unavailable')).toBeInTheDocument();
  });

  it('keeps a confirmed import and reports a follow-up refresh failure separately', async () => {
    vi.mocked(api.loadProcedures)
      .mockResolvedValueOnce(procedures)
      .mockRejectedValueOnce(new Error('Refresh failed'));
    render(<App />);
    await screen.findByText('Document control');
    const file = new File(['legacy_id,title'], 'procedures.csv', { type: 'text/csv' });

    fireEvent.change(screen.getByLabelText('Legacy CSV'), { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: 'Import records' }));

    expect(await screen.findByRole('status')).toHaveTextContent(
      'Import complete: 1 created, 1 updated, 0 rejected',
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The import was saved, but the latest dashboard data could not be refreshed.',
    );
    expect(screen.getByText('Document control')).toBeInTheDocument();
    expect(screen.getByText('Data unavailable')).toBeInTheDocument();
    expect(api.importProcedures).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('button', { name: 'Import records' })).toBeEnabled();
  });

  it('reports an import API rejection without announcing a completed import', async () => {
    vi.mocked(api.importProcedures).mockRejectedValue(new Error('Required CSV column missing.'));
    render(<App />);
    await screen.findByText('Document control');
    const file = new File(['legacy_id,title'], 'procedures.csv', { type: 'text/csv' });

    fireEvent.change(screen.getByLabelText('Legacy CSV'), { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: 'Import records' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Required CSV column missing.');
    expect(screen.queryByText(/Import complete:/)).not.toBeInTheDocument();
    expect(api.loadProcedures).toHaveBeenCalledTimes(1);
    expect(screen.getByText('Document control')).toBeInTheDocument();
  });

  it('renders status through the reusable native Web Component', async () => {
    render(<App />);
    await screen.findByText('Document control');

    const badges = document.querySelectorAll('workflow-status-badge');
    expect(badges).toHaveLength(2);
    expect(badges[0].shadowRoot?.textContent).toContain('Draft');
    expect(badges[1].shadowRoot?.textContent).toContain('Active');
  });

  it('has no automated axe accessibility violations in its loaded state', async () => {
    const { container } = render(<App />);
    await screen.findByText('Document control');

    const results = await axe.run(container, {
      rules: {
        'color-contrast': { enabled: false },
      },
    });
    expect(results.violations).toHaveLength(0);
  });
});
