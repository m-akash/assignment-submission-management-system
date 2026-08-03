import Link from 'next/link';
import { ArrowUpRight, type LucideIcon } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

const accent = {
  neutral: 'bg-muted text-muted-foreground',
  primary: 'bg-primary/10 text-primary',
  info: 'bg-info-muted text-info',
  success: 'bg-success-muted text-success',
  warning: 'bg-warning-muted text-warning',
  danger: 'bg-danger-muted text-danger',
} as const;

export function StatCard({
  label,
  value,
  hint,
  icon: Icon,
  tone = 'neutral',
  loading,
  href,
}: {
  label: string;
  value: number | string;
  hint?: string;
  icon: LucideIcon;
  tone?: keyof typeof accent;
  loading?: boolean;
  /** Where the tile drills into. Given one, the card becomes the link — a count the
   *  reader cannot act on is a dead end. */
  href?: string;
}) {
  const body = (
    <div className="flex items-start justify-between gap-3">
      <div className="min-w-0 space-y-1">
        <p className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{label}</p>
        {loading ? (
          <Skeleton className="h-8 w-16" />
        ) : (
          <p className="text-3xl font-semibold tabular-nums">{value}</p>
        )}
        {hint && <p className="truncate text-xs text-muted-foreground">{hint}</p>}
      </div>
      <div className={cn('flex size-9 shrink-0 items-center justify-center rounded-lg', accent[tone])}>
        <Icon className="size-4.5" />
      </div>
    </div>
  );

  if (!href) {
    return <div className="rounded-xl border bg-card p-5">{body}</div>;
  }

  return (
    <Link
      href={href}
      className="group relative rounded-xl border bg-card p-5 transition-colors hover:border-primary/40 hover:bg-accent/40"
    >
      {body}
      <ArrowUpRight className="absolute right-4 bottom-4 size-4 text-muted-foreground opacity-0 transition-opacity group-hover:opacity-100" />
    </Link>
  );
}
