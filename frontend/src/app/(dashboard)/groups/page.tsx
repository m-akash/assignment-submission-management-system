'use client';

import { useState } from 'react';
import { Layers, MoreHorizontal, Pencil, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ConfirmDialog } from '@/components/shared/confirm-dialog';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { GroupFormDialog } from '@/components/features/admin/group-form-dialog';
import { useDeleteGroup, useGroups } from '@/hooks/use-admin-resources';
import type { Group } from '@/types/api';

export default function GroupsPage() {
  return (
    <RoleGuard allow={['Admin']}>
      <GroupsView />
    </RoleGuard>
  );
}

function GroupsView() {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Group | null>(null);
  const [deleting, setDeleting] = useState<Group | null>(null);

  const remove = useDeleteGroup();
  const query = useGroups({ search, page, pageSize: 10 });
  const items = query.data?.items ?? [];

  function openCreate() {
    setEditing(null);
    setFormOpen(true);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Groups"
        description="The streams a student picks from class IX — Science, Humanities, Business Studies. Students in one class can be in different groups."
        actions={
          <Button onClick={openCreate}>
            <Plus className="size-4" />
            Create group
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
            placeholder="Search groups…"
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
                    <TableHead>Name</TableHead>
                    <TableHead>Code</TableHead>
                    <TableHead className="w-20">Action</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.isLoading ? (
                    <TableSkeleton columns={3} />
                  ) : items.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={3} className="p-0">
                        <EmptyState
                          icon={Layers}
                          title={search ? 'Nothing matches that search' : 'No groups yet'}
                          description={
                            search
                              ? undefined
                              : 'Create the groups your class IX and above students can be placed in.'
                          }
                          action={
                            !search && (
                              <Button size="sm" onClick={openCreate}>
                                <Plus className="size-4" />
                                Create group
                              </Button>
                            )
                          }
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    items.map((group) => (
                      <TableRow key={group.id}>
                        <TableCell className="font-medium">{group.name}</TableCell>
                        <TableCell>
                          <Badge variant="secondary" className="font-mono">
                            {group.code}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button
                                variant="ghost"
                                size="icon"
                                aria-label={`Actions for ${group.name}`}
                              >
                                <MoreHorizontal className="size-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem
                                onClick={() => {
                                  setEditing(group);
                                  setFormOpen(true);
                                }}
                              >
                                <Pencil className="size-4" />
                                Edit
                              </DropdownMenuItem>
                              <DropdownMenuItem
                                variant="destructive"
                                onClick={() => setDeleting(group)}
                              >
                                <Trash2 className="size-4" />
                                Delete
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </TableCell>
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
                itemLabel="groups"
              />
            )}
          </>
        )}
      </div>

      <GroupFormDialog open={formOpen} onOpenChange={setFormOpen} group={editing} />

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(open) => !open && setDeleting(null)}
        title="Delete this group?"
        description={`"${deleting?.name}" can only be deleted once no students belong to it.`}
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
