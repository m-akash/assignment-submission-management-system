import Link from 'next/link';
import { ArrowUpRight, type LucideIcon } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

/** One tone per tile, applied to the icon chip, the corner wash and the meter together. */
const TONES = {
  neutral: {
    chip: 'bg-muted text-muted-foreground ring-border',
    wash: 'bg-foreground/5',
    meter: 'bg-muted-foreground',
  },
  primary: {
    chip: 'bg-primary/10 text-primary ring-primary/20',
    wash: 'bg-primary/15',
    meter: 'bg-primary',
  },
  info: {
    chip: 'bg-info-muted text-info ring-info/20',
    wash: 'bg-info/15',
    meter: 'bg-info',
  },
  success: {
    chip: 'bg-success-muted text-success ring-success/20',
    wash: 'bg-success/15',
    meter: 'bg-success',
  },
  warning: {
    chip: 'bg-warning-muted text-warning ring-warning/20',
    wash: 'bg-warning/15',
    meter: 'bg-warning',
  },
  danger: {
    chip: 'bg-danger-muted text-danger ring-danger/20',
    wash: 'bg-danger/15',
    meter: 'bg-danger',
  },
} as const;

export function StatCard({
  label,
  value,
  hint,
  icon: Icon,
  tone = 'neutral',
  loading,
  href,
  progress,
}: {
  label: string;
  value: number | string;
  hint?: string;
  icon: LucideIcon;
  tone?: keyof typeof TONES;
  loading?: boolean;
  /** Where the tile drills into. Given one, the card becomes the link — a count the
   *  reader cannot act on is a dead end. */
  href?: string;
  /** 0–100. Draws a meter under the value, for counts that are a share of a whole. */
  progress?: number;
}) {
  const { chip, wash, meter } = TONES[tone];

  const body = (
    <>
      {/* A soft wash of the tile's own tone, so a grid of tiles reads as a palette
          rather than six identical boxes. */}
      <div
        aria-hidden
        className={cn(
          'pointer-events-none absolute -top-14 -right-10 size-32 rounded-full opacity-70 blur-2xl',
          wash,
        )}
      />

      <div className="relative flex items-start justify-between gap-3">
        <div className="min-w-0 space-y-2">
          <p className="eyebrow flex items-center gap-1">
            <span className="truncate">{label}</span>
            {href && (
              <ArrowUpRight className="size-3 opacity-0 transition-opacity group-hover:opacity-100" />
            )}
          </p>
          {loading ? (
            <Skeleton className="h-8 w-16" />
          ) : (
            <p className="font-heading text-[2rem] leading-none font-semibold tabular-nums">
              {value}
            </p>
          )}
          {hint && <p className="truncate text-xs text-muted-foreground">{hint}</p>}
        </div>
        <div
          className={cn(
            'flex size-10 shrink-0 items-center justify-center rounded-xl ring-1 ring-inset',
            chip,
          )}
        >
          <Icon className="size-4.5" />
        </div>
      </div>

      {progress !== undefined && !loading && (
        <div className="relative mt-4 h-1.5 overflow-hidden rounded-full bg-muted">
          <div
            className={cn('h-full rounded-full transition-[width] duration-700', meter)}
            style={{ width: `${Math.min(100, Math.max(0, progress))}%` }}
          />
        </div>
      )}
    </>
  );

  if (!href) {
    return <div className="panel group relative overflow-hidden p-5">{body}</div>;
  }

  return (
    <Link href={href} className="panel-interactive group relative overflow-hidden p-5">
      {body}
    </Link>
  );
}
