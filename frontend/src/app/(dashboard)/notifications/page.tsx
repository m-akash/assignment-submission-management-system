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
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { FilterSelect } from '@/components/shared/filter-select';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { StatCard } from '@/components/shared/stat-card';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import {
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

const TYPES: { value: NotificationType; label: string }[] = [
  { value: 'AssignmentPublished', label: 'Assignment published' },
  { value: 'SubmissionReceived', label: 'Submission received' },
  { value: 'SubmissionGraded', label: 'Submission graded' },
];

const TYPE_LABELS: Record<NotificationType, string> = {
  AssignmentPublished: 'Assignment published',
  SubmissionReceived: 'Submission received',
  SubmissionGraded: 'Submission graded',
};

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

  const summary = useNotificationSummary();
  const dispatch = useDispatchNotifications();
  const retry = useRetryNotification();
  const query = useNotifications({
    search,
    status: status as NotificationStatus | '',
    type: type as NotificationType | '',
    page,
    pageSize: 15,
  });

  const items = query.data?.items ?? [];
  const hasFilters = !!search || !!status || !!type;

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Administration"
        title="Email notifications"
        icon={Mail}
        description="Every notification is queued when it happens and sent by a background sweep, so nothing is lost if the mail server is unreachable."
        actions={
          <Button onClick={() => dispatch.mutate()} disabled={dispatch.isPending}>
            {dispatch.isPending ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <Send className="size-4" />
            )}
            Send queued now
          </Button>
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
            onChange={(value) => {
              setSearch(value);
              setPage(1);
            }}
            placeholder="Search by recipient or subject…"
            className="sm:max-w-xs"
          />
          <FilterSelect
            value={status}
            onChange={(value) => {
              setStatus(value);
              setPage(1);
            }}
            allLabel="Any status"
            options={STATUSES}
          />
          <FilterSelect
            value={type}
            onChange={(value) => {
              setType(value);
              setPage(1);
            }}
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
                    <TableHead>Recipient</TableHead>
                    <TableHead>Event</TableHead>
                    <TableHead>Subject</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Queued</TableHead>
                    <TableHead className="w-20">Action</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.isLoading ? (
                    <TableSkeleton columns={6} />
                  ) : items.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={6} className="p-0">
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
                      <TableRow key={notification.id}>
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
                itemLabel="notifications"
              />
            )}
          </>
        )}
      </div>
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
