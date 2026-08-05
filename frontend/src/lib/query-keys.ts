/**
 * One place for every cache key, so an invalidation after a mutation cannot miss a
 * list that happens to be on screen. Keys are hierarchical: invalidating
 * `assignments.all` clears every filtered assignment list at once.
 */
export const queryKeys = {
  me: ['me'] as const,

  users: {
    all: ['users'] as const,
    list: (filters: unknown) => ['users', 'list', filters] as const,
  },
  classes: {
    all: ['classes'] as const,
    list: (filters: unknown) => ['classes', 'list', filters] as const,
    options: ['classes', 'options'] as const,
  },
  courses: {
    all: ['courses'] as const,
    list: (filters: unknown) => ['courses', 'list', filters] as const,
    options: ['courses', 'options'] as const,
  },
  classCourses: {
    all: ['class-courses'] as const,
    list: (filters: unknown) => ['class-courses', 'list', filters] as const,
    options: ['class-courses', 'options'] as const,
  },
  enrollments: {
    all: ['enrollments'] as const,
    list: (filters: unknown) => ['enrollments', 'list', filters] as const,
  },
  studentCourses: {
    all: ['student-courses'] as const,
    list: (filters: unknown) => ['student-courses', 'list', filters] as const,
  },
  notifications: {
    all: ['notifications'] as const,
    list: (filters: unknown) => ['notifications', 'list', filters] as const,
    summary: ['notifications', 'summary'] as const,
  },
  teacherMappings: {
    all: ['teacher-mappings'] as const,
    list: (filters: unknown) => ['teacher-mappings', 'list', filters] as const,
    mine: ['teacher-mappings', 'mine'] as const,
  },
  assignments: {
    all: ['assignments'] as const,
    list: (filters: unknown) => ['assignments', 'list', filters] as const,
    detail: (id: string) => ['assignments', 'detail', id] as const,
  },
  submissions: {
    all: ['submissions'] as const,
    list: (filters: unknown) => ['submissions', 'list', filters] as const,
    detail: (id: string) => ['submissions', 'detail', id] as const,
    mine: ['submissions', 'mine'] as const,
  },
} as const;
