/**
 * The course fields every row that names a course carries — offerings, mappings,
 * assignments. The mirror of `ClassRef` in `./classes`.
 */
export interface CourseRef {
  courseId: string;
  courseName: string;
  courseCode: string;
}

/** A course as a filter dropdown wants it. */
export interface CourseOption {
  id: string;
  name: string;
  code: string;
}

/**
 * The distinct courses named by such rows, by name. A teacher's course list is not fetched
 * from `/courses` — it is whatever their teaching mappings mention — so this turns those
 * rows into the shape the filters take, the same way `distinctClasses` does for classes.
 */
export function distinctCourses(rows: readonly CourseRef[]): CourseOption[] {
  const byId = new Map<string, CourseOption>();
  for (const row of rows) {
    if (!byId.has(row.courseId)) {
      byId.set(row.courseId, { id: row.courseId, name: row.courseName, code: row.courseCode });
    }
  }
  return [...byId.values()].sort((a, b) => a.name.localeCompare(b.name));
}
