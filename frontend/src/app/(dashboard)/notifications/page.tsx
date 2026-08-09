'use client';

import { useState } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  Clock,
  Loader2,
  Mail,
  RefreshCw,
  Send,
  Trash2,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { ConfirmDialog } from '@/components/shared/confirm-dialog';
import { FilterSelect } from '@/components/shared/filter-select';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { StatCard } from '@/components/shared/stat-card';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import {
  useBulkDeleteNotifications,
  useDeleteNotification,
  useDispatchNotifications,
  useNotificationSummary,
  useNotifications,
  useRetryNotification,
} from '@/hooks/use-notifications';
import { formatDateTime } from '@/lib/format';
import type { AppNotification, NotificationStatus, NotificationType } from '@/types/api';

const STATUSES: { value: NotificationStatus; label: string }[] = [
  { value: 'Pending', label: 'Pending' },
  { value: 'Sent', label: 'Sent' },
  { value: 'Failed', label: 'Failed' },
];

const TYPE_LABELS: Record<NotificationType, string> = {
  AssignmentPublished: 'Assignment published',
  SubmissionReceived: 'Submission received',
  SubmissionGraded: 'Submission graded',
  TeacherAssignedToCourse: 'Teacher assigned to course',
  StudentEnrolled: 'Student enrolled',
  AccountCreated: 'Account created',
};

/** Filter options, derived from the labels so the two can never drift apart. */
const TYPES: { value: NotificationType; label: string }[] = (
  Object.entries(TYPE_LABELS) as [NotificationType, string][]
).map(([value, label]) => ({ value, label }));

export default function NotificationsPage() {
  return (
    <RoleGuard allow={['Admin']}>
      <NotificationsView />
    </RoleGuard>
  );
}

/**
 * The email outbox.
 *
 * Notifications are written in the same transaction as the change that caused them, then
 * sent by a background sweep — so this screen is where an admin sees what went out, what is
 * waiting, and what failed and why. That inspectability is the reason the outbox exists at
 * all rather than emails being fired inline.
 */
function NotificationsView() {
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [type, setType] = useState('');
  const [page, setPage] = useState(1);

  // Selection lives across renders but is reset whenever the view onto the list changes — a
  // checkbox ticked for a row on page 1 should never silently follow you to page 5.
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  // One confirm dialog drives both the per-row and bulk actions; `id` distinguishes them.
  const [confirmTarget, setConfirmTarget] = useState<{ kind: 'single' | 'bulk'; id?: string } | null>(
    null,
  );

  const summary = useNotificationSummary();
  const dispatch = useDispatchNotifications();
  const retry = useRetryNotification();
  const deleteOne = useDeleteNotification();
  const bulkDelete = useBulkDeleteNotifications();
  const query = useNotifications({
    search,
    status: status as NotificationStatus | '',
    type: type as NotificationType | '',
    page,
    pageSize: 15,
  });

  const items = query.data?.items ?? [];
  const hasFilters = !!search || !!status || !!type;

  const clearSelection = () => setSelectedIds(new Set());

  const changeSearch = (value: string) => {
    setSearch(value);
    setPage(1);
    clearSelection();
  };
  const changeStatus = (value: string) => {
    setStatus(value);
    setPage(1);
    clearSelection();
  };
  const changeType = (value: string) => {
    setType(value);
    setPage(1);
    clearSelection();
  };
  const changePage = (next: number) => {
    setPage(next);
    clearSelection();
  };

  const toggleRow = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  // Select-all is scoped to the rows currently on screen, not the whole outbox.
  const allOnPageSelected = items.length > 0 && items.every((item) => selectedIds.has(item.id));
  const someOnPageSelected = items.some((item) => selectedIds.has(item.id)) && !allOnPageSelected;
  const toggleAllOnPage = () => {
    setSelectedIds((prev) => {
      if (allOnPageSelected) {
        const next = new Set(prev);
        items.forEach((item) => next.delete(item.id));
        return next;
      }
      const next = new Set(prev);
      items.forEach((item) => next.add(item.id));
      return next;
    });
  };

  const confirmPending = deleteOne.isPending || bulkDelete.isPending;

  const handleConfirmDelete = () => {
    if (!confirmTarget) {
      return;
    }
    if (confirmTarget.kind === 'single' && confirmTarget.id) {
      deleteOne.mutate(confirmTarget.id);
    } else if (confirmTarget.kind === 'bulk') {
      bulkDelete.mutate([...selectedIds], { onSettled: clearSelection });
    }
    setConfirmTarget(null);
  };

  return (
    <div className="space-y-6">
      <PageHeader
        back={{ href: '/', label: 'Dashboard' }}
        eyebrow="Administration"
        title="Email notifications"
        description="Every notification is queued when it happens and sent by a background sweep, so nothing is lost if the mail server is unreachable."
        actions={
          <div className="flex items-center gap-2">
            {selectedIds.size > 0 && (
              <Button
                variant="destructive"
                onClick={() => setConfirmTarget({ kind: 'bulk' })}
                disabled={confirmPending}
              >
                {bulkDelete.isPending ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <Trash2 className="size-4" />
                )}
                Delete selected ({selectedIds.size})
              </Button>
            )}
            <Button onClick={() => dispatch.mutate()} disabled={dispatch.isPending}>
              {dispatch.isPending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Send className="size-4" />
              )}
              Send queued now
            </Button>
          </div>
        }
      />

      <div className="grid gap-4 sm:grid-cols-3">
        <StatCard
          label="Pending"
          value={summary.data?.pending ?? 0}
          icon={Clock}
          tone="warning"
          loading={summary.isLoading}
          hint="Waiting for the next sweep"
        />
        <StatCard
          label="Sent"
          value={summary.data?.sent ?? 0}
          icon={CheckCircle2}
          tone="success"
          loading={summary.isLoading}
        />
        <StatCard
          label="Failed"
          value={summary.data?.failed ?? 0}
          icon={AlertTriangle}
          tone="danger"
          loading={summary.isLoading}
          hint="Every retry used up — retry once the cause is fixed"
        />
      </div>

      <div className="panel overflow-hidden">
        <div className="flex flex-col gap-3 border-b p-4 sm:flex-row">
          <SearchInput
            value={search}
            onChange={changeSearch}
            placeholder="Search by recipient or subject…"
            className="sm:max-w-xs"
          />
          <FilterSelect
            value={status}
            onChange={changeStatus}
            allLabel="Any status"
            options={STATUSES}
          />
          <FilterSelect
            value={type}
            onChange={changeType}
            allLabel="Any event"
            options={TYPES}
            className="w-full sm:w-56"
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
                    <TableHead className="w-10">
                      <Checkbox
                        aria-label="Select all on this page"
                        checked={
                          allOnPageSelected ? true : someOnPageSelected ? 'indeterminate' : false
                        }
                        onCheckedChange={toggleAllOnPage}
                        disabled={items.length === 0}
                      />
                    </TableHead>
                    <TableHead>Recipient</TableHead>
                    <TableHead>Event</TableHead>
                    <TableHead>Subject</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Queued</TableHead>
                    <TableHead className="w-24">Action</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.isLoading ? (
                    <TableSkeleton columns={7} />
                  ) : items.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={7} className="p-0">
                        <EmptyState
                          icon={Mail}
                          title={hasFilters ? 'Nothing matches those filters' : 'No notifications yet'}
                          description={
                            hasFilters
                              ? undefined
                              : 'Publishing an assignment, submitting work, or grading a submission queues one.'
                          }
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    items.map((notification) => (
                      <TableRow key={notification.id} data-state={selectedIds.has(notification.id) ? 'selected' : undefined}>
                        <TableCell>
                          <Checkbox
                            aria-label={`Select notification to ${notification.recipientEmail}`}
                            checked={selectedIds.has(notification.id)}
                            onCheckedChange={() => toggleRow(notification.id)}
                          />
                        </TableCell>
                        <TableCell>
                          <p className="font-medium">{notification.recipientName}</p>
                          <p className="text-sm text-muted-foreground">
                            {notification.recipientEmail}
                          </p>
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {TYPE_LABELS[notification.type]}
                        </TableCell>
                        <TableCell className="max-w-xs truncate">{notification.subject}</TableCell>
                        <TableCell>
                          <DeliveryBadge notification={notification} />
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {formatDateTime(notification.createdAtUtc)}
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-1">
                            {/* Only a failed row can be retried: pending is already queued, and
                                re-sending a delivered email would just duplicate it. */}
                            {notification.status === 'Failed' && (
                              <Button
                                variant="ghost"
                                size="icon"
                                aria-label={`Retry the notification to ${notification.recipientEmail}`}
                                disabled={retry.isPending}
                                onClick={() => retry.mutate(notification.id)}
                              >
                                <RefreshCw className="size-4" />
                              </Button>
                            )}
                            <Button
                              variant="ghost"
                              size="icon"
                              aria-label={`Delete the notification to ${notification.recipientEmail}`}
                              disabled={deleteOne.isPending}
                              onClick={() =>
                                setConfirmTarget({ kind: 'single', id: notification.id })
                              }
                            >
                              <Trash2 className="size-4" />
                            </Button>
                          </div>
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
                onPageChange={changePage}
                itemLabel="notifications"
              />
            )}
          </>
        )}
      </div>

      {/* One confirm dialog for both delete paths. Destructive actions ask first — nothing
          here is undoable from the UI, and bulk delete removes several rows at once. */}
      <ConfirmDialog
        open={confirmTarget !== null}
        onOpenChange={(open) => {
          if (!open) setConfirmTarget(null);
        }}
        title={
          confirmTarget?.kind === 'bulk'
            ? `Delete ${selectedIds.size} notification${selectedIds.size === 1 ? '' : 's'}?`
            : 'Delete this notification?'
        }
        description={
          confirmTarget?.kind === 'bulk'
            ? 'The selected rows will be hidden from the outbox and will never be sent. This cannot be undone.'
            : 'It will be hidden from the outbox and never sent. This cannot be undone.'
        }
        confirmLabel="Delete"
        onConfirm={handleConfirmDelete}
        pending={confirmPending}
      />
    </div>
  );
}

/**
 * Status, with the failure reason attached. A bare "Failed" is unactionable — the error the
 * mail server gave back is the only thing that says what to change.
 */
function DeliveryBadge({ notification }: { notification: AppNotification }) {
  if (notification.status === 'Sent') {
    return <Badge variant="secondary">Sent</Badge>;
  }

  if (notification.status === 'Pending') {
    return (
      <Badge variant="outline">
        Pending
        {notification.attemptCount > 0 && ` · ${notification.attemptCount} tried`}
      </Badge>
    );
  }

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Badge variant="destructive" className="cursor-help">
          Failed · {notification.attemptCount} tried
        </Badge>
      </TooltipTrigger>
      <TooltipContent className="max-w-sm">
        {notification.lastError ?? 'No error was recorded.'}
      </TooltipContent>
    </Tooltip>
  );
}
