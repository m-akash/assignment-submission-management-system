'use client';

import { useState } from 'react';
import { Link2, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ConfirmDialog } from '@/components/shared/confirm-dialog';
import { FilterSelect } from '@/components/shared/filter-select';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { TeacherMappingFormDialog } from '@/components/features/admin/teacher-mapping-form-dialog';
import {
  useClassOptions,
  useDeleteTeacherMapping,
  useSubjectOptions,
  useTeacherMappings,
} from '@/hooks/use-admin-resources';
import type { TeacherMapping } from '@/types/api';

export default function TeacherMappingsPage() {
  return (
    <RoleGuard allow={['Admin']}>
      <MappingsView />
    </RoleGuard>
  );
}

function MappingsView() {
  const [search, setSearch] = useState('');
  const [subjectId, setSubjectId] = useState('');
  const [classId, setClassId] = useState('');
  const [page, setPage] = useState(1);
  const [formOpen, setFormOpen] = useState(false);
  const [deleting, setDeleting] = useState<TeacherMapping | null>(null);

  const subjects = useSubjectOptions();
  const classes = useClassOptions();
  const remove = useDeleteTeacherMapping();
  const query = useTeacherMappings({ search, subjectId, classId, page, pageSize: 10 });
  const items = query.data?.items ?? [];
  const isFiltered = !!search || !!subjectId || !!classId;
  const subjectOptions = (subjects.data ?? []).map((s) => ({ value: s.id, label: s.name }));
  const classOptions = (classes.data ?? []).map((c) => ({ value: c.id, label: c.name }));

  function withPageReset<T>(setter: (value: T) => void) {
    return (value: T) => {
      setter(value);
      setPage(1);
    };
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Teaching assignments"
        description="Each row authorises one teacher to create assignments for one class and subject. This is the gate the assignment rules check against."
        actions={
          <Button onClick={() => setFormOpen(true)}>
            <Plus className="size-4" />
            Assign teacher
          </Button>
        }
      />

      <div className="rounded-xl border bg-card">
        <div className="flex flex-col gap-2 border-b p-4 sm:flex-row">
          <SearchInput
            value={search}
            onChange={withPageReset(setSearch)}
            placeholder="Search teacher, subject or class…"
            className="sm:max-w-xs"
          />
          <FilterSelect
            value={subjectId}
            onChange={withPageReset(setSubjectId)}
            options={subjectOptions}
            allLabel="All subjects"
          />
          <FilterSelect
            value={classId}
            onChange={withPageReset(setClassId)}
            options={classOptions}
            allLabel="All classes"
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
                    <TableHead>Teacher</TableHead>
                    <TableHead>Subject</TableHead>
                    <TableHead>Class</TableHead>
                    <TableHead className="w-10" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.isLoading ? (
                    <TableSkeleton columns={4} />
                  ) : items.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={4} className="p-0">
                        <EmptyState
                          icon={Link2}
                          title={isFiltered ? 'Nothing matches those filters' : 'No teaching assignments yet'}
                          description={
                            isFiltered
                              ? 'Try a different search term or filter.'
                              : 'Assign a teacher to a class and subject so they can create work.'
                          }
                          action={
                            !isFiltered && (
                              <Button size="sm" onClick={() => setFormOpen(true)}>
                                <Plus className="size-4" />
                                Assign teacher
                              </Button>
                            )
                          }
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    items.map((mapping) => (
                      <TableRow key={mapping.id}>
                        <TableCell>
                          <p className="font-medium">{mapping.teacherName}</p>
                          <p className="text-xs text-muted-foreground">{mapping.teacherEmail}</p>
                        </TableCell>
                        <TableCell>
                          <p>{mapping.subjectName}</p>
                          <p className="font-mono text-xs text-muted-foreground">{mapping.subjectCode}</p>
                        </TableCell>
                        <TableCell>{mapping.className}</TableCell>
                        <TableCell>
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={`Remove ${mapping.teacherName} from ${mapping.className}`}
                            disabled={remove.isPending}
                            onClick={() => setDeleting(mapping)}
                          >
                            <Trash2 className="size-4 text-danger" />
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>

            {query.data && (
              <PaginationBar pagination={query.data.pagination} onPageChange={setPage} itemLabel="assignments" />
            )}
          </>
        )}
      </div>

      <TeacherMappingFormDialog open={formOpen} onOpenChange={setFormOpen} />

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(open) => !open && setDeleting(null)}
        title="Remove this teaching assignment?"
        description={
          deleting
            ? `${deleting.teacherName} will no longer be able to create assignments for ${deleting.className} · ${deleting.subjectName}.`
            : ''
        }
        confirmLabel="Remove"
        pending={remove.isPending}
        onConfirm={() => {
          if (deleting) {
            remove.mutate(deleting.id, { onSuccess: () => setDeleting(null) });
          }
        }}
      />
    </div>
  );
}
