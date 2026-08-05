'use client';

import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { PaginationMeta } from '@/types/api';

export function PaginationBar({
  pagination,
  onPageChange,
  itemLabel = 'items',
}: {
  pagination: PaginationMeta;
  onPageChange: (page: number) => void;
  itemLabel?: string;
}) {
  const { page, pageSize, total, totalPages } = pagination;
  if (total === 0) return null;

  const from = (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);

  return (
    <div className="flex flex-col-reverse items-center justify-between gap-3 rounded-b-xl border-t bg-muted/25 px-4 py-3 sm:flex-row">
      <p className="text-xs text-muted-foreground">
        Showing <span className="font-medium tabular-nums text-foreground">{from}</span>–
        <span className="font-medium tabular-nums text-foreground">{to}</span> of{' '}
        <span className="font-medium tabular-nums text-foreground">{total}</span> {itemLabel}
      </p>
      <div className="flex items-center gap-1.5">
        <span className="rounded-md border bg-card px-2 py-1 text-xs text-muted-foreground tabular-nums">
          Page <span className="font-medium text-foreground">{page}</span> of{' '}
          {Math.max(totalPages, 1)}
        </span>
        <Button
          variant="outline"
          size="icon"
          onClick={() => onPageChange(page - 1)}
          disabled={page <= 1}
          aria-label="Previous page"
        >
          <ChevronLeft className="size-4" />
        </Button>
        <Button
          variant="outline"
          size="icon"
          onClick={() => onPageChange(page + 1)}
          disabled={page >= totalPages}
          aria-label="Next page"
        >
          <ChevronRight className="size-4" />
        </Button>
      </div>
    </div>
  );
}
