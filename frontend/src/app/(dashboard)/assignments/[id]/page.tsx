'use client';

import { useParams } from 'next/navigation';
import { RoleGuard } from '@/components/shared/role-guard';
import { StudentAssignmentDetail } from '@/components/features/assignments/student-assignment-detail';

/**
 * A student's view of one assignment. Teachers manage their own work from the list and
 * its submissions inbox, so this route is the student's alone.
 */
export default function AssignmentDetailPage() {
  const { id } = useParams<{ id: string }>();

  return (
    <RoleGuard allow={['Student']}>
      <StudentAssignmentDetail assignmentId={id} />
    </RoleGuard>
  );
}
