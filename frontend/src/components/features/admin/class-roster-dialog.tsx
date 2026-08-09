'use client';

import { useMemo, useState } from 'react';
import { Loader2, UserMinus, UserPlus, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { MultiCombobox } from '@/components/ui/combobox';
import { Skeleton } from '@/components/ui/skeleton';
import { EmptyState, ErrorState } from '@/components/shared/states';
import {
  useCreateEnrollment,
  useDeleteEnrollment,
  useEnrollments,
  useUsers,
} from '@/hooks/use-admin-resources';
import type { ClassRoom } from '@/types/api';

/**
 * Who's in a class, and the place to change it.
 *
 * Enrollments are edited here rather than on the user form because the rules are about the
 * class: a student cannot be enrolled twice, and cannot be removed from their only class.
 * Both are enforced by the server and surface here as a toast.
 */
export function ClassRosterDialog({
  open,
  onOpenChange,
  classRoom,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  classRoom: ClassRoom | null;
}) {
  const [selectedStudentIds, setSelectedStudentIds] = useState<string[]>([]);

  const enabled = open && !!classRoom;
  const roster = useEnrollments({ classId: classRoom?.id, pageSize: 100 }, { enabled });
  // Every student, so the picker can offer the ones not already here.
  const allStudents = useUsers({ role: 'Student', pageSize: 200 }, { enabled });

  const enrol = useCreateEnrollment();
  const remove = useDeleteEnrollment();

  // Memoised so the `??` fallback does not produce a new array identity on every render,
  // which would make the derived list below recompute each time.
  const enrolled = useMemo(() => roster.data?.items ?? [], [roster.data]);

  // Filtered client-side: the API has no "students not in class X" query, and offering
  // someone already enrolled would only produce a 409.
  const addable = useMemo(() => {
    const already = new Set(enrolled.map((entry) => entry.studentId));
    return (allStudents.data?.items ?? []).filter((student) => !already.has(student.id));
  }, [enrolled, allStudents.data]);

  /**
   * Enrols everyone picked, one request at a time. Sequential rather than parallel: each
   * enrollment is checked against the class as it stands, and the server's rules are easier
   * to reason about when the writes do not race.
   *
   * A failure stops the run — the mutation has already reported it as a toast — and the
   * selection is trimmed to whoever is left, so retrying does not re-send the ones that
   * went through and collect a 409 for each.
   */
  async function onEnrol() {
    if (!classRoom || selectedStudentIds.length === 0) return;

    const pending = [...selectedStudentIds];
    try {
      while (pending.length > 0) {
        await enrol.mutateAsync({ studentId: pending[0], classId: classRoom.id });
        pending.shift();
      }
    } catch {
      // Reported by the mutation itself; here it only ends the run.
    }
    setSelectedStudentIds(pending);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{classRoom?.name ?? 'Students'}</DialogTitle>
          <DialogDescription>
            {enrolled.length} student{enrolled.length === 1 ? '' : 's'} enrolled. A student must
            belong to at least one class, so add them to the new one before removing this.
          </DialogDescription>
        </DialogHeader>

        <div className="flex items-end gap-2">
          <div className="min-w-0 flex-1">
            <MultiCombobox
              values={selectedStudentIds}
              onChange={setSelectedStudentIds}
              options={addable.map((student) => ({
                value: student.id,
                label: student.fullName,
                hint: student.studentId ?? student.email,
              }))}
              aria-label="Students to enrol"
              placeholder={
                allStudents.isLoading
                  ? 'Loading…'
                  : addable.length === 0
                    ? 'Every student is already enrolled'
                    : 'Choose students to enrol'
              }
              searchPlaceholder="Search name, ID or email…"
              emptyMessage="No students match"
              className="w-full"
            />
          </div>
          <Button onClick={onEnrol} disabled={selectedStudentIds.length === 0 || enrol.isPending}>
            {enrol.isPending ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <UserPlus className="size-4" />
            )}
            Enrol{selectedStudentIds.length > 1 ? ` ${selectedStudentIds.length}` : ''}
          </Button>
        </div>

        <div className="max-h-96 overflow-y-auto">
          {roster.isError ? (
            <ErrorState
              message={roster.error instanceof Error ? roster.error.message : undefined}
            />
          ) : roster.isLoading ? (
            <div className="space-y-3 py-2">
              {Array.from({ length: 3 }).map((_, index) => (
                <Skeleton key={index} className="h-10 w-full" />
              ))}
            </div>
          ) : enrolled.length === 0 ? (
            <EmptyState icon={Users} title="No students in this class yet" />
          ) : (
            <ul className="divide-y">
              {enrolled.map((entry) => (
                <li key={entry.id} className="flex items-center justify-between gap-3 py-3">
                  <div className="min-w-0">
                    <p className="truncate font-medium">{entry.studentName}</p>
                    <p className="truncate text-sm text-muted-foreground">{entry.studentEmail}</p>
                  </div>
                  <div className="flex shrink-0 items-center gap-2">
                    {entry.studentNumber && (
                      <span className="font-mono text-xs text-muted-foreground">
                        {entry.studentNumber}
                      </span>
                    )}
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={`Remove ${entry.studentName} from this class`}
                      disabled={remove.isPending}
                      onClick={() => remove.mutate(entry.id)}
                    >
                      <UserMinus className="size-4" />
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
