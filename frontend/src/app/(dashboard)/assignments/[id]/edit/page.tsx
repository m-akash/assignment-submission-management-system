'use client';

import Link from 'next/link';
import { useParams } from 'next/navigation';
import { ClipboardList } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { DetailSkeleton } from '@/components/shared/detail';
import { BackLink } from '@/components/shared/page-header';
import { RoleGuard } from '@/components/shared/role-guard';
import { EmptyState, ErrorState } from '@/components/shared/states';
import { AssignmentFormView } from '@/components/features/assignments/assignment-form-view';
import { useAssignment } from '@/hooks/use-assignments';
import { ApiError } from '@/lib/api';

/** Revising an assignment. The API allows only its author, so this is teachers only. */
export default function EditAssignmentPage() {
  const { id } = useParams<{ id: string }>();

  return (
    <RoleGuard allow={['Teacher']}>
      <EditView assignmentId={id} />
    </RoleGuard>
  );
}

function EditView({ assignmentId }: { assignmentId: string }) {
  const { data: assignment, isLoading, isError, error } = useAssignment(assignmentId);

  if (isError) {
    const missing = error instanceof ApiError && (error.status === 404 || error.status === 403);

    return (
      <div className="space-y-6">
        <BackLink href="/assignments" label="All assignments" />
        {missing ? (
          <EmptyState
            icon={ClipboardList}
            className="panel"
            title="This assignment is not available"
            description="It may have been deleted, or it belongs to a teacher other than you."
            action={
              <Button asChild size="sm" variant="outline">
                <Link href="/assignments">Back to assignments</Link>
              </Button>
            }
          />
        ) : (
          <ErrorState
            className="panel"
            title="Could not load this assignment"
            message={error instanceof Error ? error.message : undefined}
          />
        )}
      </div>
    );
  }

  if (isLoading || !assignment) return <DetailSkeleton />;

  return <AssignmentFormView assignment={assignment} />;
}
