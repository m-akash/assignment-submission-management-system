'use client';

import { useParams } from 'next/navigation';
import { useAuth } from '@/context/AuthContext';
import { RoleGuard } from '@/components/shared/role-guard';
import { SubmissionDetail } from '@/components/features/submissions/submission-detail';

/**
 * Marking one submission. Students reach their own work through the assignment itself,
 * so this route belongs to the people who mark it — read-only for an admin.
 */
export default function SubmissionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();

  return (
    <RoleGuard allow={['Admin', 'Teacher']}>
      <SubmissionDetail submissionId={id} readOnly={user?.role === 'Admin'} />
    </RoleGuard>
  );
}
