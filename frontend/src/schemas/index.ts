import { z } from 'zod';

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

const roleEnum = z.enum(['Admin', 'Teacher', 'Student']);

export const userSchema = z
  .object({
    fullName: z.string().trim().min(2, 'Enter the full name').max(150, 'Name is too long'),
    email: z.string().trim().min(1, 'Email is required').email('Enter a valid email address'),
    role: roleEnum,
    classId: z.string().optional(),
    departmentId: z.string().optional(),
    groupId: z.string().optional(),
    password: z.string().optional(),
    /** Set by the form, not the user: an update may leave the password untouched. */
    isEdit: z.boolean(),
    /** Also set by the form — mirrors `hasGroups` on the class the user picked, so the
     *  rule below never has to guess where the grade threshold sits. */
    classHasGroups: z.boolean(),
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
    if (values.role === 'Student' && !values.classId) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['classId'],
        message: 'A student must belong to a class',
      });
    }
    if (values.role === 'Teacher' && !values.departmentId) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['departmentId'],
        message: 'A teacher must belong to a department',
      });
    }
    if (values.role === 'Student' && values.classHasGroups && !values.groupId) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['groupId'],
        message: 'Students in this class must choose a group',
      });
    }
  });
export type UserValues = z.infer<typeof userSchema>;

export const classSchema = z.object({
  name: z.string().trim().min(2, 'Enter a class name').max(150, 'Name is too long'),
  level: z.coerce
    .number({ invalid_type_error: 'Enter a grade number' })
    .int('Enter a whole number')
    .min(1, 'Grade must be between 1 and 12')
    .max(12, 'Grade must be between 1 and 12'),
  section: z.string().trim().max(50).optional(),
});
export type ClassValues = z.infer<typeof classSchema>;

export const groupSchema = z.object({
  name: z.string().trim().min(2, 'Enter a group name').max(150, 'Name is too long'),
  // Short by design, like the department code.
  code: z
    .string()
    .trim()
    .min(2, 'Enter a group code')
    .max(10, 'Code cannot exceed 10 characters')
    .regex(/^[A-Za-z0-9-]+$/, 'Use letters, numbers and hyphens only'),
});
export type GroupValues = z.infer<typeof groupSchema>;

export const departmentSchema = z.object({
  name: z.string().trim().min(2, 'Enter a department name').max(150, 'Name is too long'),
  // Kept short because teacher ids embed it ("INS-SCI-01").
  code: z
    .string()
    .trim()
    .min(2, 'Enter a department code')
    .max(10, 'Code cannot exceed 10 characters')
    .regex(/^[A-Za-z0-9-]+$/, 'Use letters, numbers and hyphens only'),
});
export type DepartmentValues = z.infer<typeof departmentSchema>;

export const courseSchema = z.object({
  name: z.string().trim().min(2, 'Enter a course name').max(150, 'Name is too long'),
  code: z
    .string()
    .trim()
    .min(2, 'Enter a course code')
    .max(30, 'Code cannot exceed 30 characters')
    .regex(/^[A-Za-z0-9-]+$/, 'Use letters, numbers and hyphens only'),
  departmentId: z.string().min(1, 'Choose a department'),
  /** Empty string means "open to everyone" — coerced to null before it reaches the API. */
  groupId: z.string().optional(),
});
export type CourseValues = z.infer<typeof courseSchema>;

export const teacherMappingSchema = z.object({
  teacherId: z.string().min(1, 'Choose a teacher'),
  courseId: z.string().min(1, 'Choose a course'),
  classId: z.string().min(1, 'Choose a class'),
});
export type TeacherMappingValues = z.infer<typeof teacherMappingSchema>;

export const assignmentSchema = z.object({
  teacherAssignmentId: z.string().min(1, 'Choose the class and course'),
  title: z.string().trim().min(3, 'Enter a title').max(200, 'Title cannot exceed 200 characters'),
  description: z.string().trim().min(1, 'Describe what students must do'),
  // datetime-local produces "YYYY-MM-DDTHH:mm" in the browser's zone.
  deadlineLocal: z.string().min(1, 'Set a deadline').refine(isAtLeastAnHourAhead, {
    message: 'The deadline must be at least an hour from now',
  }),
  maxMarks: z.coerce
    .number({ invalid_type_error: 'Enter a number' })
    .positive('Maximum marks must be greater than zero')
    .max(1000, 'That is unusually high — check the value'),
  allowResubmission: z.boolean(),
});
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
      .number({ invalid_type_error: 'Enter a number' })
      .min(0, 'Marks cannot be negative')
      .max(maxMarks, `Marks cannot exceed ${maxMarks}`),
    feedback: z.string().trim().max(2000, 'Feedback cannot exceed 2000 characters').optional(),
  });
}
export type ReviewValues = z.infer<ReturnType<typeof reviewSchema>>;
