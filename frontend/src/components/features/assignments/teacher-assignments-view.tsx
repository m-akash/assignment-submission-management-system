'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { CheckCircle2, ClipboardList, Eye, Inbox, MoreHorizontal, Pencil, Plus, Send, Trash2 } from 'lucide-react';
import { useAuth } from '@/context/AuthContext';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ClassFilter } from '@/components/shared/class-picker';
import { ConfirmDialog } from '@/components/shared/confirm-dialog';
import { FilterSelect } from '@/components/shared/filter-select';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { AssignmentStatusBadge, DeadlineBadge } from '@/components/shared/status-badge';
import { useAssignments, useDeleteAssignment, usePublishAssignment } from '@/hooks/use-assignments';
import { useMyTeacherMappings } from '@/hooks/use-admin-resources';
import { distinctClasses } from '@/lib/classes';
import { distinctCourses } from '@/lib/courses';
import { deadlineUrgency, formatDateTime, gradeLabel, sectionLabel } from '@/lib/format';
import { richTextToPlainText } from '@/lib/rich-text';
import type { Assignment, AssignmentStatus } from '@/types/api';

const STATUS_OPTIONS = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Published', label: 'Published' },
];

export function TeacherAssignmentsView() {
  const { user } = useAuth();
  const readOnly = user?.role === 'Admin';
  const router = useRouter();

  const [search, setSearch] = useState('');
  const [statuses, setStatuses] = useState<AssignmentStatus[]>([]);
  const [classIds, setClassIds] = useState<string[]>([]);
  // One selection behind two boxes. A course and its code are the same choice said two
  // ways, so picking MATH101 in the code box is picking Mathematics — keeping them as one
  // piece of state is what stops the pair expressing a combination that matches nothing.
  const [courseIds, setCourseIds] = useState<string[]>([]);
  const [page, setPage] = useState(1);

  const [deleting, setDeleting] = useState<Assignment | null>(null);

  const mappings = useMyTeacherMappings();
  const publish = usePublishAssignment();
  const remove = useDeleteAssignment();

  const query = useAssignments({
    search,
    status: statuses,
    classId: classIds,
    courseId: courseIds,
    page,
    pageSize: 10,
  });

  // Distinct classes and courses the teacher actually teaches — no point offering the rest.
  const taughtClasses = distinctClasses(mappings.data ?? []);
  const taughtCourses = distinctCourses(mappings.data ?? []);

  /**
   * Opens the assignment's own page when a row is clicked. Skips clicks that originate
   * inside an interactive control (button, link, or dropdown menu item) so those keep
   * their own behaviour.
   */
  function openRow(assignment: Assignment) {
    return (event: React.MouseEvent) => {
      const target = event.target as HTMLElement;
      if (target.closest('a, button, [role="menuitem"], [role="menu"]')) {
        return;
      }
      router.push(`/assignments/${assignment.id}`);
    };
  }

  /** Any filter change invalidates the current page number. */
  function withPageReset<T>(setter: (value: T) => void) {
    return (value: T) => {
      setter(value);
      setPage(1);
    };
  }

  const items = query.data?.items ?? [];
  const isFiltered =
    !!search || statuses.length > 0 || classIds.length > 0 || courseIds.length > 0;

  return (
    <div className="space-y-6">
      <PageHeader
        back={{ href: '/', label: 'Dashboard' }}
        eyebrow="Coursework"
        title="Assignments"
        description={
          readOnly
            ? 'Browse assignments across the school.'
            : 'Create work as a draft, then publish it when students should see it.'
        }
        actions={
          !readOnly && (
            <Button asChild>
              <Link href="/assignments/new">
                <Plus className="size-4" />
                New assignment
              </Link>
            </Button>
          )
        }
      />

      <div className="panel overflow-hidden">
        <div className="flex flex-col gap-2 border-b p-4 sm:flex-row sm:flex-wrap">
          <SearchInput
            value={search}
            onChange={withPageReset(setSearch)}
            placeholder="Search any column…"
            className="sm:max-w-xs"
          />
          <FilterSelect
            values={statuses}
            onChange={withPageReset((values: string[]) => setStatuses(values as AssignmentStatus[]))}
            options={STATUS_OPTIONS}
            allLabel="Any status"
            className="w-44"
          />
          <FilterSelect
            values={courseIds}
            onChange={withPageReset(setCourseIds)}
            options={taughtCourses.map((course) => ({ value: course.id, label: course.name }))}
            allLabel="All courses"
            disabled={mappings.isLoading}
            className="w-44"
          />
          <FilterSelect
            values={courseIds}
            onChange={withPageReset(setCourseIds)}
            options={taughtCourses.map((course) => ({ value: course.id, label: course.code }))}
            allLabel="All codes"
            disabled={mappings.isLoading}
            className="w-36"
          />
          <ClassFilter
            classes={taughtClasses}
            loading={mappings.isLoading}
            onChange={withPageReset(setClassIds)}
          />
        </div>

        {query.isError ? (
          <ErrorState message={query.error instanceof Error ? query.error.message : undefined} />
        ) : (
          <>
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Title</TableHead>
                    <TableHead>Class</TableHead>
                    <TableHead>Section</TableHead>
                    <TableHead>Course</TableHead>
                    <TableHead>Code</TableHead>
                    <TableHead>Deadline</TableHead>
                    <TableHead className="text-right">Marks</TableHead>
                    <TableHead className="text-right">Submissions</TableHead>
                    <TableHead>Status</TableHead>
                    {!readOnly && <TableHead>Publish</TableHead>}
                    {!readOnly && <TableHead className="w-20">Action</TableHead>}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.isLoading ? (
                    <TableSkeleton columns={readOnly ? 9 : 11} />
                  ) : items.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={readOnly ? 9 : 11} className="p-0">
                        <EmptyState
                          icon={ClipboardList}
                          title={isFiltered ? 'Nothing matches those filters' : 'No assignments yet'}
                          description={
                            isFiltered
                              ? 'Try a different search term, course, class or status.'
                              : 'Create your first assignment as a draft, then publish it.'
                          }
                          action={
                            !isFiltered &&
                            !readOnly && (
                              <Button asChild size="sm">
                                <Link href="/assignments/new">
                                  <Plus className="size-4" />
                                  New assignment
                                </Link>
                              </Button>
                            )
                          }
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    items.map((assignment) => (
                      <TableRow
                        key={assignment.id}
                        onClick={openRow(assignment)}
                        className="cursor-pointer"
                      >
                        <TableCell className="max-w-[260px]">
                          <p className="truncate font-medium">{assignment.title}</p>
                          <p className="truncate text-xs text-muted-foreground">
                            {richTextToPlainText(assignment.description)}
                          </p>
                        </TableCell>
                        <TableCell className="text-sm">{gradeLabel(assignment.classLevel)}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {sectionLabel(assignment.classSection)}
                        </TableCell>
                        <TableCell className="text-sm">{assignment.courseName}</TableCell>
                        <TableCell className="font-mono text-xs text-muted-foreground">
                          {assignment.courseCode}
                        </TableCell>
                        <TableCell>
                          <DeadlineBadge urgency={deadlineUrgency(assignment.deadlineUtc)}>
                            {formatDateTime(assignment.deadlineUtc)}
                          </DeadlineBadge>
                        </TableCell>
                        <TableCell className="text-right tabular-nums">{assignment.maxMarks}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {assignment.submissionCount > 0 ? (
                            <Link
                              href={`/submissions?assignmentId=${assignment.id}`}
                              className="font-medium text-primary hover:underline"
                            >
                              {assignment.submissionCount}
                            </Link>
                          ) : (
                            <span className="text-muted-foreground">0</span>
                          )}
                        </TableCell>
                        <TableCell>
                          <AssignmentStatusBadge status={assignment.status} />
                        </TableCell>
                        {!readOnly && (
                          <TableCell>
                            {assignment.status === 'Draft' ? (
                              <Button
                                size="sm"
                                onClick={() => publish.mutate(assignment.id)}
                                disabled={publish.isPending}
                              >
                                <Send className="size-4" />
                                Publish
                              </Button>
                            ) : (
                              <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
                                <CheckCircle2 className="size-4 text-success" />
                                Live
                              </span>
                            )}
                          </TableCell>
                        )}
                        {!readOnly && (
                          <TableCell>
                            <DropdownMenu>
                              <DropdownMenuTrigger asChild>
                                <Button variant="ghost" size="icon" aria-label={`Actions for ${assignment.title}`}>
                                  <MoreHorizontal className="size-4" />
                                </Button>
                              </DropdownMenuTrigger>
                              <DropdownMenuContent align="end">
                                <DropdownMenuItem asChild>
                                  <Link href={`/assignments/${assignment.id}`}>
                                    <Eye className="size-4" />
                                    Open
                                  </Link>
                                </DropdownMenuItem>
                                <DropdownMenuItem asChild>
                                  <Link href={`/assignments/${assignment.id}/edit`}>
                                    <Pencil className="size-4" />
                                    Edit
                                  </Link>
                                </DropdownMenuItem>
                                {assignment.submissionCount > 0 && (
                                  <DropdownMenuItem asChild>
                                    <Link href={`/submissions?assignmentId=${assignment.id}`}>
                                      <Inbox className="size-4" />
                                      View submissions
                                    </Link>
                                  </DropdownMenuItem>
                                )}
                                <DropdownMenuSeparator />
                                <DropdownMenuItem
                                  variant="destructive"
                                  onClick={() => setDeleting(assignment)}
                                >
                                  <Trash2 className="size-4" />
                                  Delete
                                </DropdownMenuItem>
                              </DropdownMenuContent>
                            </DropdownMenu>
                          </TableCell>
                        )}
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>

            {query.data && (
              <PaginationBar
                pagination={query.data.pagination}
                onPageChange={setPage}
                itemLabel="assignments"
              />
            )}
          </>
        )}
      </div>

      {/* Deleting is the one thing still worth interrupting for: it is immediate and
          cannot be undone, so it asks in place rather than on a page of its own. */}
      {!readOnly && (
        <ConfirmDialog
          open={!!deleting}
          onOpenChange={(open) => !open && setDeleting(null)}
          title="Delete this assignment?"
          description={`"${deleting?.title}" will be hidden from students. Submissions already made are kept.`}
          pending={remove.isPending}
          onConfirm={() => {
            if (deleting) {
              remove.mutate(deleting.id, { onSuccess: () => setDeleting(null) });
            }
          }}
        />
      )}
    </div>
  );
}
