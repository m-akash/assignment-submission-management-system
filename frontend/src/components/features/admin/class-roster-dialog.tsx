'use client';

import { Users } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { EmptyState, ErrorState } from '@/components/shared/states';
import { useUsers } from '@/hooks/use-admin-resources';
import type { ClassRoom } from '@/types/api';

/** Who's in a class — an admin drills in from the Classes list rather than every row
 * carrying its own student array (the list endpoint only returns a count). */
export function ClassRosterDialog({
  open,
  onOpenChange,
  classRoom,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  classRoom: ClassRoom | null;
}) {
  const query = useUsers(
    { role: 'Student', classId: classRoom?.id, pageSize: 100 },
    { enabled: open && !!classRoom },
  );
  const students = query.data?.items ?? [];

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{classRoom?.name ?? 'Students'}</DialogTitle>
          <DialogDescription>
            {classRoom?.studentCount ?? 0} student{classRoom?.studentCount === 1 ? '' : 's'} in this class.
          </DialogDescription>
        </DialogHeader>

        <div className="max-h-96 overflow-y-auto">
          {query.isError ? (
            <ErrorState message={query.error instanceof Error ? query.error.message : undefined} />
          ) : query.isLoading ? (
            <div className="space-y-3 py-2">
              {Array.from({ length: 3 }).map((_, index) => (
                <Skeleton key={index} className="h-10 w-full" />
              ))}
            </div>
          ) : students.length === 0 ? (
            <EmptyState icon={Users} title="No students in this class yet" />
          ) : (
            <ul className="divide-y">
              {students.map((student) => (
                <li key={student.id} className="flex items-center justify-between gap-3 py-3">
                  <div className="min-w-0">
                    <p className="truncate font-medium">{student.fullName}</p>
                    <p className="truncate text-sm text-muted-foreground">{student.email}</p>
                  </div>
                  {!student.isActive && (
                    <span className="shrink-0 rounded-full border bg-muted px-2.5 py-0.5 text-xs font-medium text-muted-foreground">
                      Inactive
                    </span>
                  )}
                </li>
              ))}
            </ul>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
