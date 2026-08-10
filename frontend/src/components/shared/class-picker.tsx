'use client';

import { useState } from 'react';

import { Combobox, MultiCombobox } from '@/components/ui/combobox';
import { Label } from '@/components/ui/label';
import { gradesWithClasses, sectionsInGrade } from '@/lib/classes';
import { gradeLabel, sectionLabel } from '@/lib/format';
import type { ClassRoom } from '@/types/api';

/**
 * Choosing a class in two steps: the grade, then the sections that grade actually has.
 *
 * A cohort is a grade and a section, and this is the only way either is chosen anywhere in
 * the app. The second dropdown is populated off the back of the first — pick 9 and it holds
 * exactly the sections that exist for grade 9 — so a combination that is not a real class
 * cannot be expressed, and nobody has to read a list of every cohort in the school to find
 * the one they want.
 *
 * Both flavours need somewhere to keep "a grade, but no section yet": that is a legitimate
 * half-made choice with no class id to represent it. Neither uses an effect for it — the
 * grade is derived from the chosen class whenever there is one, and only falls back to what
 * was clicked while there is not, so a late-arriving option list or a reopened dialog needs
 * no synchronising.
 */

/** The grade half, shared by both flavours below. */
function GradeSelect({
  id,
  grades,
  value,
  onChange,
  disabled,
  placeholder,
  invalid,
  clearable = false,
}: {
  id: string;
  grades: number[];
  value: number | null;
  onChange: (level: number | null) => void;
  disabled?: boolean;
  placeholder: string;
  invalid?: boolean;
  clearable?: boolean;
}) {
  return (
    <Combobox
      id={id}
      value={value === null ? '' : String(value)}
      onChange={(next) => onChange(next ? Number(next) : null)}
      options={grades.map((level) => ({ value: String(level), label: gradeLabel(level) }))}
      placeholder={placeholder}
      searchPlaceholder="Search grades…"
      emptyMessage="No classes yet"
      disabled={disabled}
      clearable={clearable}
      aria-label="Class"
      aria-invalid={invalid}
    />
  );
}

/**
 * One class, for a form field. The value is the class id the API wants; the two dropdowns are
 * how it is arrived at.
 */
export function ClassPicker({
  classes,
  loading,
  value,
  onChange,
  invalid,
  idPrefix = 'class',
}: {
  classes: readonly ClassRoom[];
  loading?: boolean;
  /** The chosen class id, or "" while the choice is incomplete. */
  value: string;
  onChange: (classId: string) => void;
  invalid?: boolean;
  /** Distinguishes the two field ids when more than one picker shares a form. */
  idPrefix?: string;
}) {
  // Only consulted between picking a grade and picking a section — once a class is chosen it
  // is the class that says which grade this is, which is what keeps the two from disagreeing.
  const [pendingGrade, setPendingGrade] = useState<number | null>(null);

  const selected = classes.find((classRoom) => classRoom.id === value) ?? null;
  const grade = selected ? selected.level : pendingGrade;

  const grades = gradesWithClasses(classes);
  const sections = sectionsInGrade(classes, grade);

  return (
    <div className="grid grid-cols-2 gap-3">
      <div className="space-y-2">
        <Label htmlFor={`${idPrefix}-grade`}>Class</Label>
        <GradeSelect
          id={`${idPrefix}-grade`}
          grades={grades}
          value={grade}
          onChange={(level) => {
            setPendingGrade(level);
            // The chosen section belongs to the old grade, so it cannot survive the change.
            onChange('');
          }}
          disabled={loading}
          placeholder={loading ? 'Loading…' : 'Choose a class'}
          invalid={invalid}
          // Both halves clear back to nothing chosen, the same way the filters do. A form
          // field is worth undoing: the grade narrows the sections, so picking the wrong
          // one otherwise leaves no way back to the full list.
          clearable
        />
      </div>

      <div className="space-y-2">
        <Label htmlFor={`${idPrefix}-section`}>Section</Label>
        <Combobox
          id={`${idPrefix}-section`}
          value={value}
          onChange={onChange}
          options={sections.map((classRoom) => ({
            value: classRoom.id,
            label: sectionLabel(classRoom.section),
          }))}
          placeholder={grade === null ? 'Choose a class first' : 'Choose a section'}
          searchPlaceholder="Search sections…"
          emptyMessage="This class has no sections yet"
          disabled={loading || grade === null}
          aria-invalid={invalid}
          clearable
        />
      </div>
    </div>
  );
}

/**
 * The same two steps as a list filter. Emits class ids, because that is what every list
 * endpoint narrows by (`?classId=a&classId=b`).
 *
 * A grade on its own means all of its sections — the common case is "show me grade 9", and
 * making someone tick every section to say it would be busywork. Narrowing further is what
 * the section box is for.
 */
export function ClassFilter({
  classes,
  loading,
  onChange,
  initialClassIds,
  className = 'w-40',
}: {
  classes: readonly ClassRoom[];
  loading?: boolean;
  onChange: (classIds: string[]) => void;
  /** Class ids the screen was deep-linked with, so the two boxes show what is being filtered. */
  initialClassIds?: readonly string[];
  className?: string;
}) {
  const [chosenGrade, setChosenGrade] = useState<number | null>(null);
  const [chosenSectionIds, setChosenSectionIds] = useState<string[]>([]);
  const [touched, setTouched] = useState(false);

  // Until someone touches the controls they show what the screen was deep-linked with — a
  // ?classId arrives before the option list does, and boxes reading "All classes" above an
  // already-filtered list would be a lie. The parent already holds those ids as its own
  // filter state, so nothing needs emitting for them.
  const seeded =
    !touched && initialClassIds?.length
      ? classes.filter((classRoom) => initialClassIds.includes(classRoom.id))
      : [];
  const grade = touched ? chosenGrade : (seeded[0]?.level ?? null);
  const sectionIds = touched ? chosenSectionIds : seeded.map((classRoom) => classRoom.id);

  const grades = gradesWithClasses(classes);
  const sections = sectionsInGrade(classes, grade);

  // "Grade, no section" is every class in the grade; no grade at all is no filter.
  function emit(level: number | null, chosen: string[]) {
    if (level === null) return onChange([]);
    if (chosen.length > 0) return onChange(chosen);
    onChange(sectionsInGrade(classes, level).map((classRoom) => classRoom.id));
  }

  return (
    <div className="flex items-center gap-2">
      <div className={className}>
        <GradeSelect
          id="filter-grade"
          grades={grades}
          value={grade}
          onChange={(level) => {
            setTouched(true);
            setChosenGrade(level);
            setChosenSectionIds([]);
            emit(level, []);
          }}
          disabled={loading}
          placeholder="All classes"
          clearable
        />
      </div>
      <div className={className}>
        <MultiCombobox
          values={sectionIds}
          onChange={(next) => {
            setTouched(true);
            setChosenGrade(grade);
            setChosenSectionIds(next);
            emit(grade, next);
          }}
          options={sections.map((classRoom) => ({
            value: classRoom.id,
            label: sectionLabel(classRoom.section),
          }))}
          placeholder="All sections"
          searchPlaceholder="Search sections…"
          emptyMessage="This class has no sections yet"
          disabled={loading || grade === null}
          aria-label="Section"
        />
      </div>
    </div>
  );
}
