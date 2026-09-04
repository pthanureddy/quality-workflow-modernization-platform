import { createElement } from 'react';
import type { ProcedureStatus } from '../types';
import { workflowStatusBadgeTag } from './workflow-status-badge';

export function WorkflowStatusBadge({ status }: { status: ProcedureStatus }) {
  return createElement(workflowStatusBadgeTag, {
    status,
    'aria-label': `Status: ${status}`,
  });
}
