'use client';

import { useQuery } from '@tanstack/react-query';
import { apiGet, toQuery } from '@/lib/api';
import { queryKeys } from '@/lib/query-keys';
import type {
  AdminDashboardStats,
  StudentDashboardStats,
  TeacherDashboardStats,
} from '@/types/api';

/**
 * The default trend window, matching the server's. Sent explicitly rather than left to the
 * server's default so the value is part of the cache key — two windows are two charts.
 */
export const DEFAULT_TREND_DAYS = 14;

/**
 * Chart data for the overview screens. One hook per role, because the endpoints are
 * role-gated: calling the wrong one is a 403, not an empty chart. Each overview component
 * only ever mounts under its own role, so there is nothing to branch on here.
 *
 * Every series arrives pre-aggregated — see the note on the dashboard types.
 */
export function useAdminDashboard(days = DEFAULT_TREND_DAYS) {
  return useQuery({
    queryKey: queryKeys.dashboard.admin(days),
    queryFn: () => apiGet<AdminDashboardStats>(`/api/v1/dashboard/admin${toQuery({ days })}`),
  });
}

export function useTeacherDashboard(days = DEFAULT_TREND_DAYS) {
  return useQuery({
    queryKey: queryKeys.dashboard.teacher(days),
    queryFn: () => apiGet<TeacherDashboardStats>(`/api/v1/dashboard/teacher${toQuery({ days })}`),
  });
}

export function useStudentDashboard() {
  return useQuery({
    queryKey: queryKeys.dashboard.student,
    queryFn: () => apiGet<StudentDashboardStats>('/api/v1/dashboard/student'),
  });
}
