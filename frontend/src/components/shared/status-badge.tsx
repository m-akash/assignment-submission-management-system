import { CheckCircle2, Clock, FileEdit, Send, TimerOff } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { AssignmentStatus, SubmissionStatus } from '@/types/api';

/**
 * One colour per state, everywhere it appears. Statuses drive most of the scanning in
 * this app, so they are defined once here rather than re-styled per screen.
 */
const tone = {
  neutral: 'bg-muted text-muted-foreground border-transparent',
  info: 'bg-info-muted text-info border-info/25',
  success: 'bg-success-muted text-success border-success/25',
  warning: 'bg-warning-muted text-warning border-warning/25',
  danger: 'bg-danger-muted text-danger border-danger/25',
} as const;

type Tone = keyof typeof tone;

function Pill({
  children,
  variant,
  icon: Icon,
  className,
}: {
  children: React.ReactNode;
  variant: Tone;
  icon?: React.ComponentType<{ className?: string }>;
  className?: string;
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium whitespace-nowrap',
        tone[variant],
        className,
      )}
    >
      {Icon && <Icon className="size-3.5" />}
      {children}
    </span>
  );
}

const assignmentTone: Record<AssignmentStatus, { variant: Tone; icon: typeof FileEdit; label: string }> = {
  Draft: { variant: 'neutral', icon: FileEdit, label: 'Draft' },
  Published: { variant: 'info', icon: Send, label: 'Published' },
};

export function AssignmentStatusBadge({ status }: { status: AssignmentStatus }) {
  const { variant, icon, label } = assignmentTone[status];
  return (
    <Pill variant={variant} icon={icon}>
      {label}
    </Pill>
  );
}

const submissionTone: Record<SubmissionStatus, { variant: Tone; icon: typeof Clock; label: string }> = {
  Pending: { variant: 'neutral', icon: Clock, label: 'Draft' },
  Submitted: { variant: 'info', icon: Send, label: 'Submitted' },
  Graded: { variant: 'success', icon: CheckCircle2, label: 'Graded' },
  Late: { variant: 'warning', icon: TimerOff, label: 'Late' },
};

export function SubmissionStatusBadge({ status }: { status: SubmissionStatus }) {
  const { variant, icon, label } = submissionTone[status];
  return (
    <Pill variant={variant} icon={icon}>
      {label}
    </Pill>
  );
}

/** No submission row exists yet for this student. */
export function NotStartedBadge() {
  return (
    <Pill variant="neutral" icon={Clock}>
      Not started
    </Pill>
  );
}

export function RoleBadge({ role }: { role: 'Admin' | 'Teacher' | 'Student' }) {
  const variant: Tone = role === 'Admin' ? 'danger' : role === 'Teacher' ? 'info' : 'success';
  return <Pill variant={variant}>{role}</Pill>;
}

export function DeadlineBadge({ urgency, children }: { urgency: 'overdue' | 'due-soon' | 'upcoming'; children: React.ReactNode }) {
  const variant: Tone = urgency === 'overdue' ? 'danger' : urgency === 'due-soon' ? 'warning' : 'neutral';
  return (
    <Pill variant={variant} icon={Clock}>
      {children}
    </Pill>
  );
}
