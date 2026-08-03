import { differenceInHours, format, formatDistanceToNowStrict, isPast, parseISO } from 'date-fns';

/** The API sends UTC ISO strings; render them in the viewer's locale. */
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  return format(parseISO(iso), 'd MMM yyyy, HH:mm');
}

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  return format(parseISO(iso), 'd MMM yyyy');
}

export function formatRelative(iso: string | null | undefined): string {
  if (!iso) return '—';
  return formatDistanceToNowStrict(parseISO(iso), { addSuffix: true });
}

export type DeadlineUrgency = 'overdue' | 'due-soon' | 'upcoming';

/**
 * Deadlines drive most of the student UI, so urgency is computed once here rather than
 * re-derived in each component. "Soon" is within 24 hours.
 */
export function deadlineUrgency(deadlineUtc: string): DeadlineUrgency {
  const deadline = parseISO(deadlineUtc);
  if (isPast(deadline)) return 'overdue';
  return differenceInHours(deadline, new Date()) <= 24 ? 'due-soon' : 'upcoming';
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function initials(fullName: string): string {
  return fullName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join('');
}

/** "12 / 100" for marks, or an em dash when ungraded. */
export function formatMarks(marks: number | null, outOf: number | null): string {
  if (marks === null || marks === undefined) return '—';
  return outOf ? `${marks} / ${outOf}` : String(marks);
}
