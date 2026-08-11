'use client';

import { cn } from '@/lib/utils';

/**
 * A one-row stacked bar for a part-to-whole with two or three parts, with every count
 * written out beside it.
 *
 * Deliberately not a pie or a donut. Two or three slices of a circle are harder to compare
 * than two or three lengths on a shared baseline, and a reader cannot tell 45% from 55% in a
 * ring at this size — the segments here start from one edge and can be read off directly.
 *
 * The segments are steps of one hue rather than separate colours, because these scales have
 * an order — published after draft, on time before late before never. The progression is then
 * visible in the colour itself, and a reader who cannot separate hues still sees it.
 */
export function ShareBar({
  segments,
  total,
  className,
}: {
  /** In order, strongest first. Zero-count segments are dropped from the bar but kept in the key. */
  segments: readonly ShareSegment[];
  /** The whole the segments are parts of — passed in rather than summed, so a caller can
   *  show a share of something the segments do not cover. */
  total: number;
  className?: string;
}) {
  return (
    <div className={cn('space-y-4', className)}>
      {/* A 2px gap in the panel's own colour separates the segments. A stroke around each
          one would add ink that is not data; the gap does the same work with nothing. */}
      <div
        className="flex h-3 gap-0.5 overflow-hidden rounded-full bg-muted"
        role="img"
        aria-label={segments
          .map((segment) => `${segment.label}: ${segment.count} of ${total}`)
          .join(', ')}
      >
        {segments
          .filter((segment) => segment.count > 0)
          .map((segment) => (
            <div
              key={segment.label}
              className="h-full first:rounded-l-full last:rounded-r-full"
              style={{
                backgroundColor: segment.color,
                // Percentages rather than flex-grow: a segment must be its share of the
                // whole, not its share of whatever the other segments left over.
                width: total > 0 ? `${(segment.count / total) * 100}%` : '0%',
              }}
            />
          ))}
      </div>

      {/* Every value in text as well as in the bar — the bar is the shape, this is the data.
          A segment too thin to label is still readable here. */}
      <dl className="grid gap-3 sm:grid-cols-3">
        {segments.map((segment) => (
          <div key={segment.label} className="flex items-start gap-2">
            <span
              aria-hidden
              className="mt-1 size-2.5 shrink-0 rounded-[3px]"
              style={{ backgroundColor: segment.color }}
            />
            <div className="min-w-0">
              <dt className="truncate text-xs text-muted-foreground">{segment.label}</dt>
              <dd className="font-heading text-lg leading-tight font-semibold">
                {segment.count}
                {total > 0 && (
                  <span className="ml-1.5 text-xs font-normal text-muted-foreground tabular-nums">
                    {Math.round((segment.count / total) * 100)}%
                  </span>
                )}
              </dd>
            </div>
          </div>
        ))}
      </dl>
    </div>
  );
}

export interface ShareSegment {
  label: string;
  count: number;
  /** A CSS colour, normally a `var(--chart-step-*)` so it follows the theme. */
  color: string;
}

/**
 * The ordinal ramp, strongest first — "most complete" to "least". Named here so every share
 * bar steps the same way and no caller reaches for a raw token in the wrong order.
 */
export const SHARE_STEPS = [
  'var(--chart-step-1)',
  'var(--chart-step-2)',
  'var(--chart-step-3)',
] as const;
