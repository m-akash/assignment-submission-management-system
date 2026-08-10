'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { apiDelete, apiGet, apiGetBlob, apiGetPaged, apiPost, apiPostForm, apiPut, toQuery } from '@/lib/api';
import { queryKeys } from '@/lib/query-keys';
import type { Assignment, AssignmentFile, AssignmentStatus } from '@/types/api';

/**
 * Each field is named for the query parameter it becomes. Where the screen offers a
 * multi-select the value is an array, which `toQuery` repeats — `?classId=a&classId=b`,
 * matched as a union server-side. A lone value stays legal for the call sites that pin a
 * list to one id.
 */
export interface AssignmentFilters {
  search?: string;
  classId?: string | string[];
  courseId?: string | string[];
  classCourseId?: string | string[];
  status?: AssignmentStatus | '' | AssignmentStatus[];
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

/**
 * One assignment by id, for a details page. Scoping is the server's again: it answers
 * 404 for an assignment the caller may not see, so the page needs no rule of its own.
 */
export function useAssignment(id: string) {
  return useQuery({
    queryKey: queryKeys.assignments.detail(id),
    queryFn: () => apiGet<Assignment>(`/api/v1/assignments/${id}`),
    enabled: !!id,
  });
}

export interface AssignmentInput {
  /** The offering the work is for. */
  classCourseId: string;
  /** Only sent by an admin; a teacher's own request is authored from their token. */
  teacherId?: string;
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
      // Publishing queues an email to every enrolled student, so the outbox is stale.
      queryClient.invalidateQueries({ queryKey: queryKeys.notifications.all });
      toast.success('Assignment published — students have been notified');
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

// ── Attachments (teacher-uploaded reference material) ─────────────────────
// Distinct from a student's own submission files — these live on the assignment
// itself, uploaded by its owning teacher, and are visible to anyone who can see
// the assignment (same class, group and published-status rules apply).

export function useUploadAssignmentFile() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ assignmentId, file }: { assignmentId: string; file: File }) => {
      const form = new FormData();
      form.append('file', file);
      return apiPostForm<AssignmentFile>(`/api/v1/assignments/${assignmentId}/attachments/upload`, form);
    },
    onSuccess: (file) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.assignments.all });
      toast.success(`${file.originalFileName} attached`);
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

export function useDeleteAssignmentFile() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (fileId: string) => apiDelete(`/api/v1/assignments/attachments/${fileId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.assignments.all });
      toast.success('Attachment removed');
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

/**
 * The bytes of one attachment. Shared by the download below and the in-page image
 * viewer, which need the same authorized request and differ only in what they do with
 * the result — so the endpoint is written down once.
 */
export function fetchAssignmentFile(fileId: string): Promise<Blob> {
  return apiGetBlob(`/api/v1/assignments/attachments/${fileId}`);
}

/**
 * Attachments are streamed by the API after an authorization check, so they cannot be
 * linked directly — fetch the blob, then hand it to the browser.
 */
export async function downloadAssignmentFile(fileId: string, fileName: string): Promise<void> {
  try {
    const blob = await fetchAssignmentFile(fileId);
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.append(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  } catch (error) {
    toast.error(error instanceof Error ? error.message : 'Download failed');
  }
}
