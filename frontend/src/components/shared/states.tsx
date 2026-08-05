import { AlertCircle, type LucideIcon } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';
import { TableCell, TableRow } from '@/components/ui/table';
import { cn } from '@/lib/utils';

/**
 * The three states every list can be in. Sharing them keeps an empty table from looking
 * like a broken one, and a loading table from collapsing the layout.
 */
export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  className,
}: {
  icon: LucideIcon;
  title: string;
  description?: string;
  action?: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-4 px-6 py-16 text-center',
        className,
      )}
    >
      {/* Dashed, not solid: an empty state is a space waiting to be filled, and the
          outline says that before the copy does. */}
      <div className="relative flex size-14 items-center justify-center rounded-2xl border border-dashed bg-muted/40 text-muted-foreground">
        <span
          aria-hidden
          className="absolute -inset-3 rounded-3xl border border-dashed border-border/60"
        />
        <Icon className="size-6" />
      </div>
      <div className="space-y-1.5">
        <p className="font-heading font-medium">{title}</p>
        {description && (
          <p className="mx-auto max-w-sm text-sm text-muted-foreground">{description}</p>
        )}
      </div>
      {action}
    </div>
  );
}

export function ErrorState({ message, className }: { message?: string; className?: string }) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-3 px-6 py-16 text-center',
        className,
      )}
    >
      <div className="flex size-14 items-center justify-center rounded-2xl bg-danger-muted text-danger ring-1 ring-danger/20 ring-inset">
        <AlertCircle className="size-6" />
      </div>
      <div className="space-y-1.5">
        <p className="font-heading font-medium">Could not load this list</p>
        <p className="mx-auto max-w-sm text-sm text-muted-foreground">
          {message ?? 'Please try again in a moment.'}
        </p>
      </div>
    </div>
  );
}

/** Placeholder rows that keep column widths stable while a table loads. */
export function TableSkeleton({ columns, rows = 5 }: { columns: number; rows?: number }) {
  return (
    <>
      {Array.from({ length: rows }).map((_, rowIndex) => (
        <TableRow key={rowIndex}>
          {Array.from({ length: columns }).map((__, columnIndex) => (
            <TableCell key={columnIndex}>
              {/* Uneven widths read as text; equal bars read as a stalled layout. */}
              <Skeleton
                className="h-4"
                style={{ width: `${columnIndex === 0 ? 70 : 40 + ((columnIndex * 17) % 45)}%` }}
              />
            </TableCell>
          ))}
        </TableRow>
      ))}
    </>
  );
}

export function CardGridSkeleton({ count = 6 }: { count?: number }) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      {Array.from({ length: count }).map((_, index) => (
        <div key={index} className="panel space-y-3 p-5">
          <div className="flex items-center justify-between">
            <Skeleton className="h-5 w-16 rounded-full" />
            <Skeleton className="h-5 w-20 rounded-full" />
          </div>
          <Skeleton className="h-5 w-3/4" />
          <Skeleton className="h-3 w-full" />
          <Skeleton className="h-3 w-2/3" />
          <Skeleton className="h-9 w-full" />
        </div>
      ))}
    </div>
  );
}
