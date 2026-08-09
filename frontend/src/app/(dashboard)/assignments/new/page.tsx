'use client';

import { RoleGuard } from '@/components/shared/role-guard';
import { AssignmentFormView } from '@/components/features/assignments/assignment-form-view';

/**
 * Writing a new assignment. Teachers only: an admin browses Coursework read-only, and
 * work is authored by whoever teaches the class.
 */
export default function NewAssignmentPage() {
  return (
    <RoleGuard allow={['Teacher']}>
      <AssignmentFormView />
    </RoleGuard>
  );
}
