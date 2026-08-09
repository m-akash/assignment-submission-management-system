'use client';

import { useState } from 'react';
import Link from 'next/link';
import { CheckCircle2, ClipboardList, Inbox, MoreHorizontal, Pencil, Plus, Send, Trash2 } from 'lucide-react';
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
import { ConfirmDialog } from '@/components/shared/confirm-dialog';
import { FilterSelect } from '@/components/shared/filter-select';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { AssignmentStatusBadge, DeadlineBadge } from '@/components/shared/status-badge';
import { useAssignments, useDeleteAssignment, usePublishAssignment } from '@/hooks/use-assignments';
import { useMyTeacherMappings } from '@/hooks/use-admin-resources';
import { deadlineUrgency, formatDateTime } from '@/lib/format';
import { richTextToPlainText } from '@/lib/rich-text';
import { AssignmentFormDialog } from './assignment-form-dialog';
import type { Assignment, AssignmentStatus } from '@/types/api';

const STATUS_OPTIONS = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Published', label: 'Published' },
];

export function TeacherAssignmentsView() {
  const { user } = useAuth();
  const readOnly = user?.role === 'Admin';

  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<AssignmentStatus | ''>('');
  const [classId, setClassId] = useState('');
  const [page, setPage] = useState(1);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Assignment | null>(null);
  const [deleting, setDeleting] = useState<Assignment | null>(null);

  const mappings = useMyTeacherMappings();
  const publish = usePublishAssignment();
  const remove = useDeleteAssignment();

  const query = useAssignments({ search, status, classId, page, pageSize: 10 });

  // Distinct classes the teacher actually teaches — no point offering the rest.
  const classOptions = [
    ...new Map((mappings.data ?? []).map((m) => [m.classId, m.className])).entries(),
  ].map(([value, label]) => ({ value, label }));

  function openCreate() {
    setEditing(null);
    setFormOpen(true);
  }

  function openEdit(assignment: Assignment) {
    setEditing(assignment);
    setFormOpen(true);
  }

  /**
   * Opens the edit dialog when a row is clicked. Skips clicks that originate
   * inside an interactive control (button, link, or dropdown menu item) so
   * those keep their own behaviour.
   */
  function openEditRow(assignment: Assignment) {
    return (event: React.MouseEvent) => {
      const target = event.target as HTMLElement;
      if (target.closest('a, button, [role="menuitem"], [role="menu"]')) {
        return;
      }
      openEdit(assignment);
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
  const isFiltered = !!search || !!status || !!classId;

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Coursework"
        title="Assignments"
        icon={ClipboardList}
        description={
          readOnly
            ? 'Browse assignments across the school.'
            : 'Create work as a draft, then publish it when students should see it.'
        }
        actions={
          !readOnly && (
            <Button onClick={openCreate}>
              <Plus className="size-4" />
              New assignment
            </Button>
          )
        }
      />

      <div className="panel overflow-hidden">
        <div className="flex flex-col gap-2 border-b p-4 sm:flex-row">
          <SearchInput
            value={search}
            onChange={withPageReset(setSearch)}
            placeholder="Search title or description…"
            className="sm:max-w-xs"
          />
          <FilterSelect
            value={status}
            onChange={withPageReset((value: string) => setStatus(value as AssignmentStatus | ''))}
            options={STATUS_OPTIONS}
            allLabel="Any status"
            className="w-[150px]"
          />
          <FilterSelect
            value={classId}
            onChange={withPageReset(setClassId)}
            options={classOptions}
            allLabel="All classes"
            className="w-[180px]"
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
                    <TableSkeleton columns={readOnly ? 8 : 10} />
                  ) : items.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={readOnly ? 8 : 10} className="p-0">
                        <EmptyState
                          icon={ClipboardList}
                          title={isFiltered ? 'Nothing matches those filters' : 'No assignments yet'}
                          description={
                            isFiltered
                              ? 'Try a different search term or status.'
                              : 'Create your first assignment as a draft, then publish it.'
                          }
                          action={
                            !isFiltered &&
                            !readOnly && (
                              <Button onClick={openCreate} size="sm">
                                <Plus className="size-4" />
                                New assignment
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
                        onClick={openEditRow(assignment)}
                        className="cursor-pointer"
                      >
                        <TableCell className="max-w-[260px]">
                          <p className="truncate font-medium">{assignment.title}</p>
                          <p className="truncate text-xs text-muted-foreground">
                            {richTextToPlainText(assignment.description)}
                          </p>
                        </TableCell>
                        <TableCell className="text-sm">{assignment.className}</TableCell>
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
                                <DropdownMenuItem onClick={() => openEdit(assignment)}>
                                  <Pencil className="size-4" />
                                  Edit
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

      <AssignmentFormDialog
        open={formOpen}
        onOpenChange={setFormOpen}
        assignment={editing}
        readOnly={readOnly}
      />

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
