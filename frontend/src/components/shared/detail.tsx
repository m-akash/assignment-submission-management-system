'use client';

import { Download, Paperclip, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { formatBytes } from '@/lib/format';

/**
 * The parts every details page is built from. Assignments and submissions are different
 * things, but "one record, in full" is one shape: a brief, its files, and a summary rail
 * — so they are defined once here rather than per page. The way back lives on the page
 * header itself, as `BackLink` in `page-header.tsx`.
 */

/** One label/value line in a summary panel. */
export function Fact({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-4 px-5 py-3 text-sm">
      <span className="shrink-0 text-muted-foreground">{label}</span>
      <span className="min-w-0 text-right font-medium">{children}</span>
    </div>
  );
}

/**
 * A file in a list: name, size, and whichever of download/remove the caller allows.
 * Teacher materials and student attachments both use it, so both read the same way.
 */
export function FileRow({
  name,
  size,
  hint,
  onDownload,
  onRemove,
  removeDisabled,
}: {
  name: string;
  size: number;
  hint?: string;
  onDownload?: () => void;
  onRemove?: () => void;
  removeDisabled?: boolean;
}) {
  return (
    <div className="flex items-center gap-3 px-5 py-3">
      <Paperclip className="size-4 shrink-0 text-muted-foreground" />
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm">{name}</p>
        <p className="text-xs text-muted-foreground">
          {formatBytes(size)}
          {hint && ` · ${hint}`}
        </p>
      </div>
      {onDownload && (
        <Button size="icon" variant="ghost" onClick={onDownload} aria-label={`Download ${name}`}>
          <Download className="size-4" />
        </Button>
      )}
      {onRemove && (
        <Button
          size="icon"
          variant="ghost"
          disabled={removeDisabled}
          onClick={onRemove}
          aria-label={`Remove ${name}`}
        >
          <Trash2 className="size-4 text-danger" />
        </Button>
      )}
    </div>
  );
}

/** Mirrors the two-column layout so a details page does not jump once the data lands. */
export function DetailSkeleton() {
  return (
    <div className="space-y-6">
      <div className="flex items-center gap-2.5">
        <Skeleton className="size-8 rounded-lg" />
        <Skeleton className="h-4 w-32" />
      </div>
      <div className="space-y-2">
        <Skeleton className="h-3 w-40" />
        <Skeleton className="h-7 w-2/3" />
        <Skeleton className="h-3 w-52" />
      </div>
      <div className="grid gap-6 lg:grid-cols-3 lg:items-start">
        <div className="space-y-6 lg:col-span-2">
          <div className="panel space-y-3 p-5">
            <Skeleton className="h-4 w-24" />
            <Skeleton className="h-3 w-full" />
            <Skeleton className="h-3 w-11/12" />
            <Skeleton className="h-3 w-3/5" />
          </div>
          <div className="panel space-y-3 p-5">
            <Skeleton className="h-4 w-32" />
            <Skeleton className="h-32 w-full" />
            <Skeleton className="h-9 w-full" />
          </div>
        </div>
        <div className="panel space-y-4 p-5">
          {Array.from({ length: 6 }).map((_, index) => (
            <div key={index} className="flex items-center justify-between gap-4">
              <Skeleton className="h-3 w-20" />
              <Skeleton className="h-3 w-24" />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
