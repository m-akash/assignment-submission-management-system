/** Shapes returned by the ASP.NET Core API. Mirrors the backend DTOs. */

export type Role = 'Admin' | 'Teacher' | 'Student';
export type AssignmentStatus = 'Draft' | 'Published';
export type SubmissionStatus = 'Pending' | 'Submitted' | 'Graded' | 'Late';
export type NotificationStatus = 'Pending' | 'Sent' | 'Failed';
export type NotificationType =
  | 'AssignmentPublished'
  | 'SubmissionReceived'
  | 'SubmissionGraded'
  | 'TeacherAssignedToCourse'
  | 'StudentEnrolled'
  | 'AccountCreated';

/**
 * Whether an emailed password-setup link can still be used. `fullName` is present only
 * when it can — the API withholds it otherwise so a dead token reveals nothing about the
 * account it belonged to.
 */
export interface PasswordSetupStatus {
  isUsable: boolean;
  fullName: string | null;
  expiresAtUtc: string | null;
}

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

/** One class a student is enrolled in — `EnrolledClassDto` on the server. */
export interface EnrolledClass {
  enrollmentId: string;
  classId: string;
  className: string;
  classLevel: number;
  classSection: string | null;
  enrolledAtUtc: string;
}

/**
 * `UserDto` — also the payload of `GET /api/v1/auth/me`.
 *
 * `classes` is a list because membership is an enrollment relationship rather than a
 * column: empty for admins and teachers, normally one entry for a student.
 */
export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  role: Role;
  studentId: string | null;
  teacherId: string | null;
  isActive: boolean;
  createdAtUtc: string;
  classes: EnrolledClass[];
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
  /** Grade as a number, 1–12. */
  level: number;
  /** The level as a Roman numeral ("IX") — derived server-side. */
  gradeLabel: string;
  section: string | null;
  studentCount: number;
}

export interface Course {
  id: string;
  name: string;
  code: string;
}

/** A course offering — `ClassCourseDto`. The row everything else is scoped to. */
export interface ClassCourse {
  id: string;
  classId: string;
  className: string;
  classLevel: number;
  classSection: string | null;
  courseId: string;
  courseName: string;
  courseCode: string;
  /** Teachers mapped to this offering — 0 means nobody can set work for it yet. */
  teacherCount: number;
  /** Assignments created against it, drafts included. */
  assignmentCount: number;
}

/** One student's membership of one class — `EnrollmentDto`. */
export interface Enrollment {
  id: string;
  studentId: string;
  studentName: string;
  studentEmail: string;
  /** The human-readable school id ("IX-A-003"), not a Guid. */
  studentNumber: string | null;
  classId: string;
  className: string;
  classLevel: number;
  classSection: string | null;
  enrolledAtUtc: string;
}

export interface TeacherMapping {
  id: string;
  teacherId: string;
  teacherName: string;
  teacherEmail: string;
  /** The offering this mapping is for — what an assignment is scoped to. */
  classCourseId: string;
  courseId: string;
  courseName: string;
  courseCode: string;
  classId: string;
  className: string;
}

/** One teacher on a course the student takes — `StudentCourseTeacherDto`. */
export interface StudentCourseTeacher {
  teacherId: string;
  teacherName: string;
  teacherEmail: string;
}

/**
 * A course this student is enrolled in, with the teacher(s) for its offering —
 * `StudentCourseDto`. Reached through the student's class: enrollment → class → offering →
 * course, with teachers via teaching assignments.
 */
export interface StudentCourse {
  /** The offering (class↔course) id — the stable row key. */
  id: string;
  courseId: string;
  courseName: string;
  courseCode: string;
  classId: string;
  className: string;
  classLevel: number;
  classSection: string | null;
  /** Teachers mapped to this offering — empty means none assigned yet. */
  teachers: StudentCourseTeacher[];
}

export interface AssignmentFile {
  id: string;
  assignmentId: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAtUtc: string;
}

export interface Assignment {
  id: string;
  /** The offering this assignment belongs to; the class and course are flattened below. */
  classCourseId: string;
  teacherId: string;
  teacherName: string;
  courseId: string;
  courseName: string;
  courseCode: string;
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
  files: AssignmentFile[];
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

/**
 * A queued notification email — `NotificationDto`. The outbox is inspectable on purpose:
 * an admin can see exactly what was sent, or why it wasn't.
 */
export interface AppNotification {
  id: string;
  recipientId: string;
  recipientName: string;
  recipientEmail: string;
  type: NotificationType;
  subject: string;
  body: string;
  status: NotificationStatus;
  attemptCount: number;
  lastAttemptAtUtc: string | null;
  sentAtUtc: string | null;
  lastError: string | null;
  assignmentId: string | null;
  submissionId: string | null;
  createdAtUtc: string;
}

/** Counts per delivery state, for the outbox header. */
export interface NotificationSummary {
  pending: number;
  sent: number;
  failed: number;
}
