'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { apiGet, apiGetPaged, apiPost, apiDelete, toQuery } from '@/lib/api';
import { queryKeys } from '@/lib/query-keys';
import type {
  AppNotification,
  NotificationStatus,
  NotificationSummary,
  NotificationType,
} from '@/types/api';

export interface NotificationFilters {
  status?: NotificationStatus | '';
  type?: NotificationType | '';
  recipientId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

/**
 * The notification outbox. Scoping is the server's job: an admin sees the whole queue, and
 * anyone else sees only mail addressed to them — including when they pass a `recipientId`
 * for someone else.
 */
export function useNotifications(filters: NotificationFilters) {
  return useQuery({
    queryKey: queryKeys.notifications.list(filters),
    queryFn: () => apiGetPaged<AppNotification>(`/api/v1/notifications${toQuery({ ...filters })}`),
  });
}

/** Counts per delivery state. Admin-only on the server. */
export function useNotificationSummary(enabled = true) {
  return useQuery({
    queryKey: queryKeys.notifications.summary,
    queryFn: () => apiGet<NotificationSummary>('/api/v1/notifications/summary'),
    enabled,
  });
}

/**
 * Puts a failed notification back in the queue. Failed means "gave up after every retry",
 * so this is the deliberate second chance once the underlying mail problem is fixed.
 */
export function useRetryNotification() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => apiPost<AppNotification>(`/api/v1/notifications/${id}/retry`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.notifications.all });
      toast.success('Notification queued for another attempt');
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

/**
 * Runs a sweep now instead of waiting for the background timer. Exists so an admin who has
 * just corrected a mail setting — or an evaluator who does not want to wait 30 seconds —
 * can watch the queue drain.
 */
export function useDispatchNotifications() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => apiPost<{ sent: number }>('/api/v1/notifications/dispatch'),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.notifications.all });
      toast.success(
        result.sent === 0
          ? 'Nothing was waiting to be sent'
          : `${result.sent} notification${result.sent === 1 ? '' : 's'} sent`,
      );
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

/**
 * Hides one outbox row (soft delete on the server). The row vanishes from the list and from
 * the dispatcher's queue — a deleted email is never sent. Both the list and the summary
 * counts are refreshed, since removing a row shifts the pending/sent/failed totals.
 */
export function useDeleteNotification() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => apiDelete(`/api/v1/notifications/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.notifications.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.notifications.summary });
      toast.success('Notification deleted');
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

/** Hides many rows at once. Server caps the batch at 500 ids. */
export function useBulkDeleteNotifications() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (ids: string[]) =>
      apiPost<{ deleted: number }>('/api/v1/notifications/bulk-delete', { ids }),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.notifications.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.notifications.summary });
      const count = result?.deleted ?? 0;
      toast.success(`${count} notification${count === 1 ? '' : 's'} deleted`);
    },
    onError: (error: Error) => toast.error(error.message),
  });
}
