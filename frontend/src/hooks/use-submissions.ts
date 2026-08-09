'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  apiDelete,
  apiGet,
  apiGetBlob,
  apiGetPaged,
  apiPost,
  apiPostForm,
  apiPut,
  toQuery,
} from '@/lib/api';
import { queryKeys } from '@/lib/query-keys';
import { useAssignment } from './use-assignments';
import type {
  Assignment,
  Paged,
  StudentAssignment,
  Submission,
  SubmissionFile,
  SubmissionStatus,
} from '@/types/api';

export interface SubmissionFilters {
  search?: string;
  assignmentId?: string;
  status?: SubmissionStatus | '';
  page?: number;
  pageSize?: number;
}

/** Teacher/admin submission list. The server scopes it to assignments the caller owns. */
export function useSubmissions(filters: SubmissionFilters) {
  return useQuery({
    queryKey: queryKeys.submissions.list(filters),
    queryFn: () => apiGetPaged<Submission>(`/api/v1/submissions${toQuery({ ...filters })}`),
  });
}

/**
 * One submission by id, for the marking page. The server decides who may see it: the
 * student it belongs to, the teacher who set the work, or an admin.
 */
export function useSubmission(id: string) {
  return useQuery({
    queryKey: queryKeys.submissions.detail(id),
    queryFn: () => apiGet<Submission>(`/api/v1/submissions/${id}`),
    enabled: !!id,
  });
}

/**
 * The signed-in student's own submissions — one request, not one per assignment.
 * The server already restricts a student to their own rows.
 */
export function useMySubmissions() {
  return useQuery({
    queryKey: queryKeys.submissions.mine,
    queryFn: () => apiGetPaged<Submission>('/api/v1/submissions?pageSize=100'),
    select: (page: Paged<Submission>) => page.items,
  });
}

/**
 * Joins the student's assignments with their own submissions in memory.
 *
 * This used to be a request per assignment (fifty assignments meant fifty-one calls,
 * most of them 404s). Two list calls answer the same question.
 *
 * Pass `page`/`pageSize` to fetch one page at a time — the result then carries
 * `pagination` so the list view can render a pager. Omit them (the default) to pull
 * everything in a single generous page; the overview uses that mode because its
 * counts and averages need the full set.
 */
export function useStudentAssignments(filters: {
  search?: string;
  courseId?: string;
  page?: number;
  pageSize?: number;
}) {
  // Whether the caller is paging (list view) or pulling the full set (overview). Kept
  // as a runtime flag because `useQuery` must be called unconditionally — hooks rule —
  // so the paged/unpaged choice is made by what we read off `data`, not by branching.
  const pageSize = filters.pageSize ?? 100;

  const assignments = useQuery({
    queryKey: queryKeys.assignments.list({ ...filters, scope: 'student', pageSize }),
    queryFn: () =>
      apiGetPaged<Assignment>(`/api/v1/assignments${toQuery({ ...filters, pageSize })}`),
  });

  const submissions = useMySubmissions();

  const page = assignments.data;
  const paged = typeof filters.pageSize === 'number';

  const byAssignment = new Map((submissions.data ?? []).map((s) => [s.assignmentId, s]));
  const items: StudentAssignment[] = (page?.items ?? []).map((assignment) => ({
    ...assignment,
    submission: byAssignment.get(assignment.id) ?? null,
  }));

  return {
    items,
    // Only the paged caller cares about the pager; the overview never reads this.
    pagination: paged ? page?.pagination : undefined,
    isLoading: assignments.isLoading || submissions.isLoading,
    isError: assignments.isError || submissions.isError,
    error: assignments.error ?? submissions.error,
  };
}

/**
 * One assignment plus this student's own submission — the details-page counterpart of
 * `useStudentAssignments`. Both halves are already cached by the grid the student came
 * from, so opening a card usually paints from cache and revalidates behind them.
 */
export function useStudentAssignment(id: string) {
  const assignment = useAssignment(id);
  const submissions = useMySubmissions();

  const item: StudentAssignment | null = assignment.data
    ? {
        ...assignment.data,
        submission: (submissions.data ?? []).find((s) => s.assignmentId === id) ?? null,
      }
    : null;

  return {
    assignment: item,
    isLoading: assignment.isLoading || submissions.isLoading,
    isError: assignment.isError || submissions.isError,
    error: assignment.error ?? submissions.error,
  };
}

// ── Student mutations ───────────────────────────────────────────────────────

export function useSubmitAssignment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      assignmentId,
      submissionId,
      content,
    }: {
      assignmentId: string;
      submissionId?: string;
      content: string;
    }) =>
      submissionId
        ? apiPut<Submission>(`/api/v1/submissions/${submissionId}`, { content })
        : apiPost<Submission>(`/api/v1/assignments/${assignmentId}/submissions`, { content }),
    onSuccess: () => {
      invalidateSubmissionViews(queryClient);
      toast.success('Answer submitted');
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

export function useUploadSubmissionFile() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ assignmentId, file }: { assignmentId: string; file: File }) => {
      const form = new FormData();
      form.append('file', file);
      return apiPostForm<SubmissionFile>(`/api/v1/assignments/${assignmentId}/submissions/upload`, form);
    },
    onSuccess: (file) => {
      invalidateSubmissionViews(queryClient);
      toast.success(`${file.originalFileName} attached`);
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

export function useDeleteSubmissionFile() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (fileId: string) => apiDelete(`/api/v1/submissions/files/${fileId}`),
    onSuccess: () => {
      invalidateSubmissionViews(queryClient);
      toast.success('Attachment removed');
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

// ── Teacher mutations ───────────────────────────────────────────────────────

export function useReviewSubmission() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      id,
      marks,
      feedback,
      status,
    }: {
      id: string;
      marks: number;
      feedback?: string;
      status: SubmissionStatus;
    }) => apiPost<Submission>(`/api/v1/submissions/${id}/review`, { marks, feedback, status }),
    onSuccess: () => {
      invalidateSubmissionViews(queryClient);
      toast.success('Review saved');
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

// ── Download ────────────────────────────────────────────────────────────────

/**
 * Attachments are streamed by the API after an authorization check, so they cannot be
 * linked directly — fetch the blob, then hand it to the browser.
 */
export async function downloadSubmissionFile(fileId: string, fileName: string): Promise<void> {
  try {
    const blob = await apiGetBlob(`/api/v1/submissions/files/${fileId}`);
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

/** A submission change can appear in three different lists; refresh all of them. */
function invalidateSubmissionViews(queryClient: ReturnType<typeof useQueryClient>): void {
  queryClient.invalidateQueries({ queryKey: queryKeys.submissions.all });
  queryClient.invalidateQueries({ queryKey: queryKeys.assignments.all });
}
