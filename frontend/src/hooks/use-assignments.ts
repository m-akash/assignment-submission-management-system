'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { apiDelete, apiGetPaged, apiPost, apiPut, toQuery } from '@/lib/api';
import { queryKeys } from '@/lib/query-keys';
import type { Assignment, AssignmentStatus } from '@/types/api';

export interface AssignmentFilters {
  search?: string;
  classId?: string;
  subjectId?: string;
  status?: AssignmentStatus | '';
  page?: number;
  pageSize?: number;
}

/**
 * Assignment list. Scoping is the server's job: a student receives only published
 * assignments for their class, a teacher only their own. The filters here narrow that
 * set — they cannot widen it.
 */
export function useAssignments(filters: AssignmentFilters) {
  return useQuery({
    queryKey: queryKeys.assignments.list(filters),
    queryFn: () => apiGetPaged<Assignment>(`/api/v1/assignments${toQuery({ ...filters })}`),
  });
}

export interface AssignmentInput {
  teacherAssignmentId: string;
  title: string;
  description: string;
  deadlineUtc: string;
  maxMarks: number;
  allowResubmission: boolean;
}

export function useSaveAssignment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, input }: { id?: string; input: AssignmentInput }) =>
      id
        ? apiPut<Assignment>(`/api/v1/assignments/${id}`, {
            title: input.title,
            description: input.description,
            deadlineUtc: input.deadlineUtc,
            maxMarks: input.maxMarks,
            allowResubmission: input.allowResubmission,
          })
        : apiPost<Assignment>('/api/v1/assignments', input),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.assignments.all });
      toast.success(variables.id ? 'Assignment updated' : 'Draft created');
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

export function usePublishAssignment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => apiPost<Assignment>(`/api/v1/assignments/${id}/publish`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.assignments.all });
      toast.success('Assignment published — students can see it now');
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

export function useDeleteAssignment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => apiDelete(`/api/v1/assignments/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.assignments.all });
      toast.success('Assignment deleted');
    },
    onError: (error: Error) => toast.error(error.message),
  });
}
