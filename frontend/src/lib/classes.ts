import type { ClassRoom } from '@/types/api';

/**
 * A class is a grade and a section, chosen one after the other. These are the shared option
 * sets and the two derivations every class picker needs, so no screen re-invents them.
 */

/** The grades the school runs — what the create-class form offers. Numbers, never numerals. */
export const GRADE_CHOICES = [6, 7, 8, 9, 10, 11, 12] as const;

/**
 * Every section letter, A first. A–D are the ones a school actually uses, and they are the
 * first four rows of the list; the rest are there for the schools that need them and are
 * reached by typing into the picker's search box rather than scrolling past twenty-two rows.
 */
export const SECTION_CHOICES = Array.from({ length: 26 }, (_, index) =>
  String.fromCharCode(65 + index),
);

/** The class fields every row that names a class carries — offerings, mappings, enrollments. */
export interface ClassRef {
  classId: string;
  classLevel: number;
  classSection: string | null;
}

/**
 * The distinct classes named by such rows, as the pickers want them. A teacher's class list
 * is not fetched from `/classes` — it is whatever their teaching mappings mention — so this
 * turns those rows into the same shape the pickers take everywhere else.
 */
export function distinctClasses(rows: readonly ClassRef[]): ClassRoom[] {
  const byId = new Map<string, ClassRoom>();
  for (const row of rows) {
    if (!byId.has(row.classId)) {
      byId.set(row.classId, {
        id: row.classId,
        level: row.classLevel,
        section: row.classSection,
        studentCount: 0,
      });
    }
  }
  return [...byId.values()];
}

/** The grades that actually have classes, ascending — the first dropdown of a class picker. */
export function gradesWithClasses(classes: readonly ClassRoom[]): number[] {
  return [...new Set(classes.map((classRoom) => classRoom.level))].sort((a, b) => a - b);
}

/**
 * The classes sitting in one grade, by section — the second dropdown, loaded off the back of
 * the first. Sorted by section so the list reads A, B, C regardless of the order they were
 * created in.
 */
export function sectionsInGrade(classes: readonly ClassRoom[], level: number | null): ClassRoom[] {
  if (level === null) return [];
  return classes
    .filter((classRoom) => classRoom.level === level)
    .sort((a, b) => (a.section ?? '').localeCompare(b.section ?? ''));
}
