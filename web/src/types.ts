export type ProcedureStatus = 'Draft' | 'Active' | 'Archived';

export interface ProcedureRecord {
  id: string;
  legacyId: string;
  title: string;
  owner: string;
  version: string;
  status: ProcedureStatus;
  reviewDueOn: string;
  revision: number;
  updatedAt: string;
}

export interface Dashboard {
  totalProcedures: number;
  draftProcedures: number;
  activeProcedures: number;
  archivedProcedures: number;
  overdueReviews: number;
  openCorrectiveActions: number;
}

export interface ImportResult {
  created: number;
  updated: number;
  rejected: number;
  issues: Array<{ rowNumber: number; message: string }>;
}

