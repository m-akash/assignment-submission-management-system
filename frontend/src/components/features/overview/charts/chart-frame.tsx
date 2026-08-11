'use client';

import type { LucideIcon } from 'lucide-react';
import { SectionPanel } from '@/components/shared/section-panel';
import { EmptyState, ErrorState } from '@/components/shared/states';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

/**
 * The frame every chart on the overview screens sits in: the same panel the lists use, plus
 * the three states a chart can be in before it has anything to draw.
 *
 * Refetching does not fall back to the skeleton — the previous render is held at reduced
 * opacity instead. A chart that blinks back to a grey box every time the window refocuses
 * loses the reader's place and jumps the layout underneath them.
 */
export function ChartFrame({
  title,
  description,
  icon,
  action,
  isLoading,
  isFetching,
  error,
  isEmpty,
  emptyTitle,
  emptyDescription,
  emptyIcon,
  className,
  contentClassName,
  children,
}: {
  title: string;
  description?: string;
  icon?: LucideIcon;
  action?: React.ReactNode;
  /** True only before the first response — never on a background refetch. */
  isLoading?: boolean;
  isFetching?: boolean;
  error?: unknown;
  /** The request succeeded and there is genuinely nothing to plot yet. */
  isEmpty?: boolean;
  emptyTitle: string;
  emptyDescription?: string;
  emptyIcon: LucideIcon;
  className?: string;
  /** For a short chart sharing a grid row with a tall one — centre it rather than let it
   *  sit against the top of a panel stretched to its neighbour's height. */
  contentClassName?: string;
  children: React.ReactNode;
}) {
  return (
    <SectionPanel
      title={title}
      description={description}
      icon={icon}
      action={action}
      className={className}
    >
      {isLoading ? (
        <ChartSkeleton />
      ) : error ? (
        <ErrorState
          title="Could not load this chart"
          message={error instanceof Error ? error.message : undefined}
        />
      ) : isEmpty ? (
        <EmptyState icon={emptyIcon} title={emptyTitle} description={emptyDescription} />
      ) : (
        <div
          className={cn(
            'p-5 transition-opacity duration-200',
            isFetching && 'opacity-60',
            contentClassName,
          )}
        >
          {children}
        </div>
      )}
    </SectionPanel>
  );
}

/**
 * Placeholder shaped like a plot rather than a block: uneven bars read as "a chart is
 * coming", a single grey rectangle reads as a stalled panel.
 */
function ChartSkeleton() {
  const heights = [42, 68, 55, 80, 62, 90, 48];

  return (
    <div className="flex h-56 items-end gap-3 p-5" aria-hidden>
      {heights.map((height, index) => (
        <Skeleton key={index} className="flex-1 rounded-md" style={{ height: `${height}%` }} />
      ))}
    </div>
  );
}
