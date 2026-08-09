import { z } from 'zod';

import { isRichTextEmpty } from '@/lib/rich-text';

/**
 * Client-side schemas mirroring the server's request contracts. These exist for
 * immediate feedback while typing — the API re-validates everything and remains the
 * only authority. Where a rule is a real business rule (deadline at least an hour
 * ahead, marks within the maximum) it is stated here too so the user is not made to
 * submit a form just to be told.
 */

export const loginSchema = z.object({
  email: z.string().min(1, 'Email is required').email('Enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
});
export type LoginValues = z.infer<typeof loginSchema>;

/**
 * Choosing a password from an emailed setup link. Eight characters to match the bar the
 * admin create-user form sets — the API's floor is six, and being stricter here is the
 * safe direction. The confirmation field exists only client-side: the server has no use
 * for it, and a typo in a password nobody can recover is worth one extra box.
 */
export const setPasswordSchema = z
  .object({
    newPassword: z.string().min(8, 'Use at least 8 characters').max(128, 'That password is too long'),
    confirmPassword: z.string().min(1, 'Confirm your password'),
  })
  .superRefine((values, ctx) => {
    if (values.newPassword !== values.confirmPassword) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['confirmPassword'],
        message: 'Both passwords must match',
      });
    }
  });
export type SetPasswordValues = z.infer<typeof setPasswordSchema>;

const roleEnum = z.enum(['Admin', 'Teacher', 'Student']);

export const userSchema = z
  .object({
    fullName: z.string().trim().min(2, 'Enter the full name').max(150, 'Name is too long'),
    email: z.string().trim().min(1, 'Email is required').email('Enter a valid email address'),
    role: roleEnum,
    classId: z.string().optional(),
    academicYearId: z.string().optional(),
    password: z.string().optional(),
    /** Set by the form, not the user: an update may leave the password untouched. */
    isEdit: z.boolean(),
  })
  .superRefine((values, ctx) => {
    if (!values.isEdit && (values.password ?? '').length < 8) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['password'],
        message: 'Use at least 8 characters',
      });
    }
    if (values.isEdit && values.password && values.password.length < 8) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['password'],
        message: 'Use at least 8 characters, or leave blank to keep the current one',
      });
    }
    // Only on create: the field is not shown when editing, because moving a student
    // between classes goes through enrollments (which refuses to leave them with none).
    if (!values.isEdit && values.role === 'Student' && !values.classId) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['classId'],
        message: 'A student must belong to a class',
      });
    }
    // The class is meaningless without the session it is for. The API would fall back to
    // the current year, but the form shows the field, so leaving it blank is a mistake
    // rather than an omission — and a school with no current year would be refused there.
    if (!values.isEdit && values.role === 'Student' && !values.academicYearId) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['academicYearId'],
        message: 'Choose the academic year',
      });
    }
  });
export type UserValues = z.infer<typeof userSchema>;

/**
 * A school session. The name is free text rather than derived from the dates: what a
 * session is called ("2026", "2026-2027", "Session 12") is a local convention the server
 * does not try to guess, so neither does this.
 *
 * Dates are the raw "YYYY-MM-DD" a date input produces and are compared as strings — ISO
 * dates sort chronologically as text, so this needs no Date, which is what keeps the value
 * from being shifted by the reader's time zone on the way to the API.
 */
export const academicYearSchema = z
  .object({
    name: z.string().trim().min(2, 'Enter a name for the year').max(50, 'Name is too long'),
    startDate: z.string().min(1, 'Choose a start date'),
    endDate: z.string().min(1, 'Choose an end date'),
    isCurrent: z.boolean(),
  })
  .superRefine((values, ctx) => {
    if (values.startDate && values.endDate && values.endDate <= values.startDate) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['endDate'],
        message: 'The end date must be after the start date',
      });
    }
  });
export type AcademicYearValues = z.infer<typeof academicYearSchema>;

// A class is a grade and a section, and nothing else — there is no name to enter, and the
// two are never joined. The bounds match the server's: the form offers 6–12, but a class
// created outside that range is still editable rather than refused on sight.
export const classSchema = z.object({
  level: z.coerce
    .number()
    .int('Choose a grade')
    .min(1, 'Grade must be between 1 and 12')
    .max(12, 'Grade must be between 1 and 12'),
  section: z.string().trim().min(1, 'Choose a section').max(50, 'Section is too long'),
});
/**
 * A coerced field is read from the form as something looser than what validation
 * produces (a number input hands back a string), so the two sides are separate types.
 * Forms are parameterised on both: `Input` is what `register` writes, `Values` is what
 * the submit handler receives. Collapsing them only compiles by accident of the zod
 * version in use — see the same pattern on assignments and reviews below.
 */
export type ClassInput = z.input<typeof classSchema>;
export type ClassValues = z.infer<typeof classSchema>;

export const courseSchema = z.object({
  name: z.string().trim().min(2, 'Enter a course name').max(150, 'Name is too long'),
  code: z
    .string()
    .trim()
    .min(2, 'Enter a course code')
    .max(30, 'Code cannot exceed 30 characters')
    .regex(/^[A-Za-z0-9-]+$/, 'Use letters, numbers and hyphens only'),
});
export type CourseValues = z.infer<typeof courseSchema>;

/**
 * A mapping is made against an offering, not a (class, course) pair — the pair could name
 * a combination the class does not study, which is exactly what the offering rules out.
 */
export const teacherMappingSchema = z.object({
  teacherId: z.string().min(1, 'Choose a teacher'),
  classCourseId: z.string().min(1, 'Choose the class and course'),
});
export type TeacherMappingValues = z.infer<typeof teacherMappingSchema>;

export const classCourseSchema = z.object({
  classId: z.string().min(1, 'Choose a class'),
  courseId: z.string().min(1, 'Choose a course'),
});
export type ClassCourseValues = z.infer<typeof classCourseSchema>;

export const enrollmentSchema = z.object({
  studentId: z.string().min(1, 'Choose a student'),
  classId: z.string().min(1, 'Choose a class'),
  academicYearId: z.string().min(1, 'Choose the academic year'),
});
export type EnrollmentValues = z.infer<typeof enrollmentSchema>;

export const assignmentSchema = z.object({
  /**
   * The teaching mapping, not the offering: it identifies the class, the course AND the
   * teacher in one choice. A teacher sees only their own, so it reads as "which of my
   * classes"; an admin sees all of them, so it also answers "on whose behalf". The
   * submit handler unpacks it into the offering and teacher the API expects.
   */
  teachingMappingId: z.string().min(1, 'Choose the class and course'),
  title: z.string().trim().min(3, 'Enter a title').max(200, 'Title cannot exceed 200 characters'),
  /**
   * HTML from the rich-text editor. "Not empty" cannot be a length check on the raw
   * value — an editor that has been typed into and cleared still serialises to markup —
   * so emptiness is judged on the text inside the tags. The cap is on the markup rather
   * than that text because it is the markup the column has to hold; the editor stops
   * typing at 5000 characters, which no amount of formatting can inflate this far.
   */
  description: z
    .string()
    .refine((value) => !isRichTextEmpty(value), 'Describe what students must do')
    .refine((value) => value.length <= 20_000, 'That description is too long'),
  // datetime-local produces "YYYY-MM-DDTHH:mm" in the browser's zone.
  deadlineLocal: z.string().min(1, 'Set a deadline').refine(isAtLeastAnHourAhead, {
    message: 'The deadline must be at least an hour from now',
  }),
  maxMarks: z.coerce
    .number()
    .positive('Maximum marks must be greater than zero')
    .max(1000, 'That is unusually high — check the value'),
  allowResubmission: z.boolean(),
});
export type AssignmentInput = z.input<typeof assignmentSchema>;
export type AssignmentValues = z.infer<typeof assignmentSchema>;

function isAtLeastAnHourAhead(value: string): boolean {
  const deadline = new Date(value);
  if (Number.isNaN(deadline.getTime())) return false;
  return deadline.getTime() - Date.now() >= 60 * 60 * 1000;
}

export const submissionSchema = z.object({
  content: z.string().max(20_000, 'That answer is too long').optional(),
});
export type SubmissionValues = z.infer<typeof submissionSchema>;

/** Marks are bounded by the assignment, so the schema is built per assignment. */
export function reviewSchema(maxMarks: number) {
  return z.object({
    marks: z.coerce
      .number()
      .min(0, 'Marks cannot be negative')
      .max(maxMarks, `Marks cannot exceed ${maxMarks}`),
    feedback: z.string().trim().max(2000, 'Feedback cannot exceed 2000 characters').optional(),
  });
}
export type ReviewInput = z.input<ReturnType<typeof reviewSchema>>;
export type ReviewValues = z.infer<ReturnType<typeof reviewSchema>>;
