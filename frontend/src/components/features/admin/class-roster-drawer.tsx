'use client';

import { useMemo, useState } from 'react';
import { Loader2, UserMinus, UserPlus, Users, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Drawer,
  DrawerClose,
  DrawerContent,
  DrawerDescription,
  DrawerHeader,
  DrawerTitle,
} from '@/components/ui/drawer';
import { Combobox, MultiCombobox } from '@/components/ui/combobox';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { EmptyState, ErrorState } from '@/components/shared/states';
import { classLabel } from '@/lib/format';
import {
  useAcademicYearOptions,
  useCreateEnrollment,
  useCurrentAcademicYear,
  useDeleteEnrollment,
  useEnrollments,
  useUsers,
} from '@/hooks/use-admin-resources';
import type { ClassRoom } from '@/types/api';

/**
 * Who's in a class for a given session, and the place to change it.
 *
 * Enrollments are edited here rather than on the user form because the rules are about the
 * class: a student cannot be enrolled twice in the same year, and cannot be removed from
 * their only class. Both are enforced by the server and surface here as a toast.
 *
 * The year is a filter and the write target at once, deliberately: a class cohort outlives
 * a session, so "who is in Class IX-A" has no answer until one is named. Showing every year
 * at once would put a student's 2025 and 2026 rows side by side with no way to tell which
 * enrollment a Remove button was about to delete.
 *
 * The year and the add-students row sit above the scroll area rather than inside it, so a
 * class of a hundred can be scrolled without losing the controls that change it.
 */
export function ClassRosterDrawer({
  open,
  onOpenChange,
  classRoom,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  classRoom: ClassRoom | null;
}) {
  const [selectedStudentIds, setSelectedStudentIds] = useState<string[]>([]);
  // Holds only an explicit choice. The year actually in force is derived below, so the
  // drawer falls onto the current session the moment the options arrive without an effect
  // that would have to re-run to catch a cold cache.
  const [pickedYearId, setPickedYearId] = useState('');

  const academicYears = useAcademicYearOptions();
  const currentAcademicYear = useCurrentAcademicYear();
  const academicYearId = pickedYearId || currentAcademicYear?.id || '';

  /** Clears the working state on close so the next class opens clean. */
  function handleOpenChange(next: boolean) {
    if (!next) {
      setSelectedStudentIds([]);
      setPickedYearId('');
    }
    onOpenChange(next);
  }

  const enabled = open && !!classRoom && !!academicYearId;
  const roster = useEnrollments({ classId: classRoom?.id, academicYearId, pageSize: 100 }, { enabled });
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
    if (!classRoom || !academicYearId || selectedStudentIds.length === 0) return;

    const pending = [...selectedStudentIds];
    try {
      while (pending.length > 0) {
        await enrol.mutateAsync({ studentId: pending[0], classId: classRoom.id, academicYearId });
        pending.shift();
      }
    } catch {
      // Reported by the mutation itself; here it only ends the run.
    }
    setSelectedStudentIds(pending);
  }

  return (
    <Drawer open={open} onOpenChange={handleOpenChange} direction="right">
      {/* The width the drawer ships with is qualified by its direction, so overriding it
          has to repeat the qualifier or lose on specificity. */}
      <DrawerContent className="data-[vaul-drawer-direction=right]:sm:max-w-lg">
        <DrawerHeader className="border-b pr-12">
          <DrawerTitle>
            {classRoom ? classLabel(classRoom.level, classRoom.section) : 'Students'}
          </DrawerTitle>
          <DrawerDescription>
            {academicYearId
              ? `${enrolled.length} student${enrolled.length === 1 ? '' : 's'} enrolled for this year. `
              : ''}
            A student must belong to at least one class, so add them to the new one before
            removing this.
          </DrawerDescription>
        </DrawerHeader>

        <DrawerClose asChild>
          <Button variant="ghost" size="icon-sm" className="absolute top-3 right-3">
            <X />
            <span className="sr-only">Close</span>
          </Button>
        </DrawerClose>

        <div className="space-y-4 border-b p-4">
          <div className="space-y-2">
            <Label htmlFor="rosterAcademicYear">Academic year</Label>
            <Combobox
              id="rosterAcademicYear"
              value={academicYearId}
              onChange={(value) => {
                setPickedYearId(value);
                // The addable list is derived from the roster for this year, so a stale
                // selection could name someone already enrolled in the year just chosen.
                setSelectedStudentIds([]);
              }}
              options={(academicYears.data ?? []).map((year) => ({
                value: year.id,
                label: year.name,
                hint: year.isCurrent ? 'Current' : undefined,
              }))}
              placeholder={
                academicYears.isLoading
                  ? 'Loading…'
                  : (academicYears.data ?? []).length === 0
                    ? 'No academic years yet — create one first'
                    : 'Choose the academic year'
              }
              searchPlaceholder="Search academic years…"
              emptyMessage="No academic years match"
            />
          </div>

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
                disabled={!academicYearId}
                placeholder={
                  !academicYearId
                    ? 'Choose an academic year first'
                    : allStudents.isLoading
                      ? 'Loading…'
                      : addable.length === 0
                        ? 'Every student is already enrolled for this year'
                        : 'Choose students to enrol'
                }
                searchPlaceholder="Search name, ID or email…"
                emptyMessage="No students match"
                className="w-full"
              />
            </div>
            <Button
              onClick={onEnrol}
              disabled={!academicYearId || selectedStudentIds.length === 0 || enrol.isPending}
            >
              {enrol.isPending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <UserPlus className="size-4" />
              )}
              Enrol{selectedStudentIds.length > 1 ? ` ${selectedStudentIds.length}` : ''}
            </Button>
          </div>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-4">
          {!academicYearId ? (
            <EmptyState
              icon={Users}
              title="Choose an academic year"
              description="A class runs every year, so the roster is per session."
            />
          ) : roster.isError ? (
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
            <EmptyState icon={Users} title="No students in this class for this year" />
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
      </DrawerContent>
    </Drawer>
  );
}
