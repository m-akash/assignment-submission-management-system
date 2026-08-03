'use client';

import { useState } from 'react';
import { Link2, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ConfirmDialog } from '@/components/shared/confirm-dialog';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { TeacherMappingFormDialog } from '@/components/features/admin/teacher-mapping-form-dialog';
import { useDeleteTeacherMapping, useTeacherMappings } from '@/hooks/use-admin-resources';
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
  const [page, setPage] = useState(1);
  const [formOpen, setFormOpen] = useState(false);
  const [deleting, setDeleting] = useState<TeacherMapping | null>(null);

  const remove = useDeleteTeacherMapping();
  const query = useTeacherMappings({ search, page, pageSize: 10 });
  const items = query.data?.items ?? [];

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
        <div className="border-b p-4">
          <SearchInput
            value={search}
            onChange={(value) => {
              setSearch(value);
              setPage(1);
            }}
            placeholder="Search teacher, subject or class…"
            className="sm:max-w-xs"
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
                          title={search ? 'Nothing matches that search' : 'No teaching assignments yet'}
                          description={
                            search
                              ? undefined
                              : 'Assign a teacher to a class and subject so they can create work.'
                          }
                          action={
                            !search && (
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
