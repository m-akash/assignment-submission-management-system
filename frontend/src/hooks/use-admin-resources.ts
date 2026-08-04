'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { apiDelete, apiGetPaged, apiPost, apiPut, toQuery } from '@/lib/api';
import { queryKeys } from '@/lib/query-keys';
import type { ClassRoom, Course, Paged, Role, TeacherMapping, User } from '@/types/api';

/** Shared shape of every list screen's server-side filter state. */
export interface ListFilters {
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface UserFilters extends ListFilters {
  role?: Role | '';
  classId?: string;
}

// ── Users ───────────────────────────────────────────────────────────────────

export function useUsers(filters: UserFilters, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: queryKeys.users.list(filters),
    queryFn: () => apiGetPaged<User>(`/api/v1/users${toQuery({ ...filters })}`),
    enabled: options?.enabled ?? true,
  });
}

export interface UserInput {
  email: string;
  fullName: string;
  password?: string;
  role: Role;
  classId?: string | null;
}

export function useSaveUser() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, input }: { id?: string; input: UserInput }) =>
      id
        ? apiPut<User>(`/api/v1/users/${id}`, {
            fullName: input.fullName,
            password: input.password || null,
            classId: input.classId || null,
          })
        : apiPost<User>('/api/v1/users', {
            ...input,
            classId: input.classId || null,
          }),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
      toast.success(variables.id ? 'User updated' : 'User created');
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

export function useDeleteUser() {
  return useResourceDelete('/api/v1/users', queryKeys.users.all, 'User deactivated');
}

// ── Classes ─────────────────────────────────────────────────────────────────

export function useClasses(filters: ListFilters) {
  return useQuery({
    queryKey: queryKeys.classes.list(filters),
    queryFn: () => apiGetPaged<ClassRoom>(`/api/v1/classes${toQuery({ ...filters })}`),
  });
}

/** Every class, for populating pickers. Cached longer — reference data changes rarely. */
export function useClassOptions() {
  return useQuery({
    queryKey: queryKeys.classes.options,
    queryFn: () => apiGetPaged<ClassRoom>('/api/v1/classes?pageSize=100'),
    staleTime: 5 * 60 * 1000,
    select: (page: Paged<ClassRoom>) => page.items,
  });
}

export interface ClassInput {
  name: string;
  level: number;
  section?: string | null;
}

export function useSaveClass() {
  return useResourceSave<ClassRoom, ClassInput>('/api/v1/classes', queryKeys.classes.all, 'Class');
}

export function useDeleteClass() {
  return useResourceDelete('/api/v1/classes', queryKeys.classes.all, 'Class deleted');
}

// ── Courses ────────────────────────────────────────────────────────────────

export function useCourses(filters: ListFilters) {
  return useQuery({
    queryKey: queryKeys.courses.list(filters),
    queryFn: () => apiGetPaged<Course>(`/api/v1/courses${toQuery({ ...filters })}`),
  });
}

export function useCourseOptions() {
  return useQuery({
    queryKey: queryKeys.courses.options,
    queryFn: () => apiGetPaged<Course>('/api/v1/courses?pageSize=100'),
    staleTime: 5 * 60 * 1000,
    select: (page: Paged<Course>) => page.items,
  });
}

export interface CourseInput {
  name: string;
  code: string;
}

export function useSaveCourse() {
  return useResourceSave<Course, CourseInput>('/api/v1/courses', queryKeys.courses.all, 'Course');
}

export function useDeleteCourse() {
  return useResourceDelete('/api/v1/courses', queryKeys.courses.all, 'Course deleted');
}

// ── Teacher mappings ────────────────────────────────────────────────────────

export interface TeacherMappingFilters extends ListFilters {
  teacherId?: string;
  courseId?: string;
  classId?: string;
}

export function useTeacherMappings(filters: TeacherMappingFilters) {
  return useQuery({
    queryKey: queryKeys.teacherMappings.list(filters),
    queryFn: () => apiGetPaged<TeacherMapping>(`/api/v1/teacher-assignments${toQuery({ ...filters })}`),
  });
}

/**
 * The signed-in teacher's own class/course mappings — the server scopes this by role,
 * so a teacher receives only their own regardless of query parameters.
 */
export function useMyTeacherMappings(enabled = true) {
  return useQuery({
    queryKey: queryKeys.teacherMappings.mine,
    queryFn: () => apiGetPaged<TeacherMapping>('/api/v1/teacher-assignments?pageSize=100'),
    select: (page: Paged<TeacherMapping>) => page.items,
    staleTime: 5 * 60 * 1000,
    enabled,
  });
}

export function useCreateTeacherMapping() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { teacherId: string; courseId: string; classId: string }) =>
      apiPost<TeacherMapping>('/api/v1/teacher-assignments', input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.teacherMappings.all });
      toast.success('Teacher assigned');
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

export function useDeleteTeacherMapping() {
  return useResourceDelete(
    '/api/v1/teacher-assignments',
    queryKeys.teacherMappings.all,
    'Assignment removed',
  );
}

// ── Shared mutation factories ───────────────────────────────────────────────
// Classes and courses are plain create-or-update resources; sharing the wiring keeps
// each one to a single line and guarantees identical cache and toast behaviour.

function useResourceSave<TResult, TInput>(
  baseUrl: string,
  invalidateKey: readonly unknown[],
  label: string,
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, input }: { id?: string; input: TInput }) =>
      id ? apiPut<TResult>(`${baseUrl}/${id}`, input) : apiPost<TResult>(baseUrl, input),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: invalidateKey });
      toast.success(`${label} ${variables.id ? 'updated' : 'created'}`);
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

function useResourceDelete(baseUrl: string, invalidateKey: readonly unknown[], message: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => apiDelete(`${baseUrl}/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: invalidateKey });
      toast.success(message);
    },
    onError: (error: Error) => toast.error(error.message),
  });
}
