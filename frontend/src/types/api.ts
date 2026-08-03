/** Shapes returned by the ASP.NET Core API. Mirrors the backend DTOs. */

export type Role = 'Admin' | 'Teacher' | 'Student';
export type AssignmentStatus = 'Draft' | 'Published';
export type SubmissionStatus = 'Pending' | 'Submitted' | 'Graded' | 'Late';

/** Success envelope produced by `ApiResponse<T>` on the server. */
export interface ApiEnvelope<T> {
  success: boolean;
  data: T;
  message?: string | null;
  pagination?: PaginationMeta | null;
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

/** A list response with its pagination metadata kept together. */
export interface Paged<T> {
  items: T[];
  pagination: PaginationMeta;
}

/** RFC 7807 body returned for every non-2xx response. */
export interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
  type?: string;
  instance?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

/** `UserDto` — also the payload of `GET /api/v1/auth/me`. */
export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  role: Role;
  classId: string | null;
  className: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export type User = AuthUser;

/** `AuthResponseBody` — the refresh token is never in the body, only in the cookie. */
export interface LoginResponse {
  userId: string;
  email: string;
  fullName: string;
  role: Role;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
}

export interface ClassRoom {
  id: string;
  name: string;
  grade: string | null;
  section: string | null;
  studentCount: number;
}

export interface Subject {
  id: string;
  name: string;
  code: string;
}

export interface TeacherMapping {
  id: string;
  teacherId: string;
  teacherName: string;
  teacherEmail: string;
  subjectId: string;
  subjectName: string;
  subjectCode: string;
  classId: string;
  className: string;
}

export interface Assignment {
  id: string;
  teacherAssignmentId: string;
  teacherId: string;
  teacherName: string;
  subjectId: string;
  subjectName: string;
  subjectCode: string;
  classId: string;
  className: string;
  title: string;
  description: string;
  deadlineUtc: string;
  maxMarks: number;
  status: AssignmentStatus;
  allowResubmission: boolean;
  submissionCount: number;
  createdAtUtc: string;
}

export interface SubmissionFile {
  id: string;
  submissionId: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAtUtc: string;
}

export interface Submission {
  id: string;
  assignmentId: string;
  assignmentTitle: string;
  studentId: string;
  studentName: string;
  content: string | null;
  status: SubmissionStatus;
  submittedAtUtc: string | null;
  marks: number | null;
  marksOutOf: number | null;
  feedback: string | null;
  reviewedById: string | null;
  reviewedByName: string | null;
  reviewedAtUtc: string | null;
  files: SubmissionFile[];
}

/** An assignment plus this student's own submission, as the student dashboard needs it. */
export interface StudentAssignment extends Assignment {
  submission: Submission | null;
}
