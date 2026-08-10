'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { apiDelete, apiGetPaged, apiPost, apiPut, toQuery } from '@/lib/api';
import { queryKeys } from '@/lib/query-keys';
import type {
  AcademicYear,
  ClassCourse,
  ClassRoom,
  Course,
  Enrollment,
  Paged,
  Role,
  StudentCourse,
  TeacherMapping,
  User,
} from '@/types/api';

/**
 * Shared shape of every list screen's server-side filter state.
 *
 * The narrowing fields below are named for the query parameter they become, and each
 * accepts an array as well as a single value: `toQuery` repeats it
 * (`?classId=a&classId=b`) and the endpoint matches their union. A screen's multi-select
 * therefore hands its selection straight through, while the call sites that pin a list to
 * one id keep passing that id.
 */
export interface ListFilters {
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface UserFilters extends ListFilters {
  role?: Role | '' | Role[];
  classId?: string | string[];
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
  /** Only read on create: the student's first class, enrolled in the same transaction. */
  classId?: string | null;
  /** The session that first enrollment belongs to. Omitted, the server uses the current one. */
  academicYearId?: string | null;
}

export function useSaveUser() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, input }: { id?: string; input: UserInput }) =>
      id
        ? // An update is profile and password only. Moving a student between classes goes
          // through the enrollments endpoint, which refuses to leave them with none.
          apiPut<User>(`/api/v1/users/${id}`, {
            fullName: input.fullName,
            password: input.password || null,
          })
        : apiPost<User>('/api/v1/users', {
            ...input,
            classId: input.classId || null,
            academicYearId: input.academicYearId || null,
          }),
    onSuccess: (_data, variables) => {
      // Creating a student writes an enrollment too, so that list is stale as well.
      queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.enrollments.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.classes.all });
      toast.success(variables.id ? 'User updated' : 'User created');
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

export function useDeleteUser() {
  return useResourceDelete('/api/v1/users', queryKeys.users.all, 'User deactivated');
}

// ── Academic years ──────────────────────────────────────────────────────────

export function useAcademicYears(filters: ListFilters) {
  return useQuery({
    queryKey: queryKeys.academicYears.list(filters),
    queryFn: () => apiGetPaged<AcademicYear>(`/api/v1/academic-years${toQuery({ ...filters })}`),
  });
}

export function useAcademicYearOptions() {
  return useQuery({
    queryKey: queryKeys.academicYears.options,
    queryFn: () => apiGetPaged<AcademicYear>('/api/v1/academic-years?pageSize=100'),
    staleTime: 5 * 60 * 1000,
    select: (page: Paged<AcademicYear>) => page.items,
  });
}

/**
 * The session the school is running, for forms that should open on it. Undefined while the
 * options are loading and when no year is flagged current — the callers treat both the same
 * way (nothing preselected), because both mean "there is no answer yet".
 */
export function useCurrentAcademicYear() {
  const years = useAcademicYearOptions();
  return years.data?.find((year) => year.isCurrent);
}

export interface AcademicYearInput {
  name: string;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
}

export function useSaveAcademicYear() {
  return useResourceSave<AcademicYear, AcademicYearInput>(
    '/api/v1/academic-years',
    queryKeys.academicYears.all,
    'Academic year',
    // Marking a different year current changes what every enrollment form preselects, and
    // the enrollment lists render the year's name, so both go stale on any save.
    queryKeys.enrollments.all,
  );
}

export function useDeleteAcademicYear() {
  return useResourceDelete(
    '/api/v1/academic-years',
    queryKeys.academicYears.all,
    'Academic year deleted',
  );
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
  level: number;
  section: string;
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
  teacherId?: string | string[];
  courseId?: string | string[];
  classId?: string | string[];
  classCourseId?: string | string[];
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
    mutationFn: (input: { teacherId: string; classCourseId: string }) =>
      apiPost<TeacherMapping>('/api/v1/teacher-assignments', input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.teacherMappings.all });
      // The offering list shows a teacher count, so it is stale too.
      queryClient.invalidateQueries({ queryKey: queryKeys.classCourses.all });
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
    queryKeys.classCourses.all,
  );
}

// ── Course offerings (which courses a class studies) ────────────────────────

export interface ClassCourseFilters extends ListFilters {
  classId?: string | string[];
  courseId?: string | string[];
  teacherId?: string | string[];
}

export function useClassCourses(filters: ClassCourseFilters) {
  return useQuery({
    queryKey: queryKeys.classCourses.list(filters),
    queryFn: () => apiGetPaged<ClassCourse>(`/api/v1/class-courses${toQuery({ ...filters })}`),
  });
}

/**
 * Every offering, for populating pickers — the admin assignment form needs the whole
 * catalogue, unlike a teacher, who picks from their own mappings.
 */
export function useClassCourseOptions(enabled = true) {
  return useQuery({
    queryKey: queryKeys.classCourses.options,
    queryFn: () => apiGetPaged<ClassCourse>('/api/v1/class-courses?pageSize=200'),
    staleTime: 5 * 60 * 1000,
    select: (page: Paged<ClassCourse>) => page.items,
    enabled,
  });
}

export function useCreateClassCourse() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { classId: string; courseId: string }) =>
      apiPost<ClassCourse>('/api/v1/class-courses', input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.classCourses.all });
      toast.success('Course added to class');
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

export function useDeleteClassCourse() {
  return useResourceDelete('/api/v1/class-courses', queryKeys.classCourses.all, 'Offering removed');
}

// ── Enrollments (which classes a student is in) ─────────────────────────────

export interface EnrollmentFilters extends ListFilters {
  studentId?: string | string[];
  classId?: string | string[];
  academicYearId?: string | string[];
}

export function useEnrollments(filters: EnrollmentFilters, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: queryKeys.enrollments.list(filters),
    queryFn: () => apiGetPaged<Enrollment>(`/api/v1/enrollments${toQuery({ ...filters })}`),
    enabled: options?.enabled ?? true,
  });
}

export function useCreateEnrollment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { studentId: string; classId: string; academicYearId: string }) =>
      apiPost<Enrollment>('/api/v1/enrollments', input),
    onSuccess: () => {
      // Enrollment changes what a student can see and what a class counts, so both the
      // user list (which renders their classes) and the class list go stale.
      queryClient.invalidateQueries({ queryKey: queryKeys.enrollments.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.classes.all });
      toast.success('Student enrolled');
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

export function useDeleteEnrollment() {
  return useResourceDelete(
    '/api/v1/enrollments',
    queryKeys.enrollments.all,
    'Student removed from class',
    queryKeys.users.all,
    queryKeys.classes.all,
  );
}

// ── The signed-in student's own courses ─────────────────────────────────────

/**
 * The courses the signed-in student is enrolled in, each with its teacher(s). The server
 * scopes this to the student's classes, so a student receives only their own regardless of
 * query parameters. Server-side paged and searched, like the admin list endpoints.
 */
export function useMyStudentCourses(filters: ListFilters) {
  return useQuery({
    queryKey: queryKeys.studentCourses.list(filters),
    queryFn: () =>
      apiGetPaged<StudentCourse>(`/api/v1/enrollments/me/courses${toQuery({ ...filters })}`),
  });
}

// ── Shared mutation factories ───────────────────────────────────────────────
// Classes and courses are plain create-or-update resources; sharing the wiring keeps
// each one to a single line and guarantees identical cache and toast behaviour.

function useResourceSave<TResult, TInput>(
  baseUrl: string,
  invalidateKey: readonly unknown[],
  label: string,
  ...alsoInvalidate: readonly (readonly unknown[])[]
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, input }: { id?: string; input: TInput }) =>
      id ? apiPut<TResult>(`${baseUrl}/${id}`, input) : apiPost<TResult>(baseUrl, input),
    onSuccess: (_data, variables) => {
      for (const key of [invalidateKey, ...alsoInvalidate]) {
        queryClient.invalidateQueries({ queryKey: key });
      }
      toast.success(`${label} ${variables.id ? 'updated' : 'created'}`);
    },
    onError: (error: Error) => toast.error(error.message),
  });
}

/**
 * Extra keys are accepted because the junctions have knock-on effects: removing an
 * enrollment changes a class's student count, and removing a teaching mapping changes an
 * offering's teacher count.
 */
function useResourceDelete(
  baseUrl: string,
  invalidateKey: readonly unknown[],
  message: string,
  ...alsoInvalidate: readonly (readonly unknown[])[]
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => apiDelete(`${baseUrl}/${id}`),
    onSuccess: () => {
      for (const key of [invalidateKey, ...alsoInvalidate]) {
        queryClient.invalidateQueries({ queryKey: key });
      }
      toast.success(message);
    },
    onError: (error: Error) => toast.error(error.message),
  });
}
