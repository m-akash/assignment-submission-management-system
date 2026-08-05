import type { LucideIcon } from 'lucide-react';
import { cn } from '@/lib/utils';

/**
 * A titled panel: header rule, optional icon and trailing action, then whatever the
 * caller puts inside (a list, a table, an empty state). Every section on the dashboards
 * uses this so headers line up across panels of different heights.
 */
export function SectionPanel({
  title,
  description,
  icon: Icon,
  action,
  children,
  className,
  bodyClassName,
}: {
  title: string;
  description?: string;
  icon?: LucideIcon;
  action?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
  bodyClassName?: string;
}) {
  return (
    <section className={cn('panel flex flex-col overflow-hidden', className)}>
      <header className="flex items-center justify-between gap-3 border-b px-5 py-3.5">
        <div className="flex min-w-0 items-center gap-2.5">
          {Icon && (
            <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground">
              <Icon className="size-4" />
            </span>
          )}
          <div className="min-w-0">
            <h2 className="truncate font-heading text-sm font-semibold">{title}</h2>
            {description && (
              <p className="truncate text-xs text-muted-foreground">{description}</p>
            )}
          </div>
        </div>
        {action && <div className="shrink-0">{action}</div>}
      </header>
      <div className={cn('min-w-0 flex-1', bodyClassName)}>{children}</div>
    </section>
  );
}
