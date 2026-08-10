'use client';

import { useState } from 'react';
import { Check, Download, Eye, Paperclip, Pencil, Trash2, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { formatBytes } from '@/lib/format';
import { MAX_BASE_NAME_LENGTH, sanitizeBaseName, splitFileName } from '@/lib/file-name';

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
 * A file in a list: name, size, and whichever of view/download/remove/rename the caller
 * allows. Teacher materials and student attachments both use it, so both read the same way.
 *
 * `onView` is passed only for files that have an inline view — images, in practice; see
 * `isViewableImage` in `file-preview.tsx`. Everything else keeps download as its one
 * action rather than opening something a browser cannot render.
 *
 * `onRename` is passed only for files still staged in the browser, where a correction is
 * free. It edits the name and nothing else: the extension is shown beside the field but
 * never inside it, because it describes the bytes rather than labelling them.
 */
export function FileRow({
  name,
  size,
  hint,
  onView,
  onDownload,
  onRemove,
  onRename,
  removeDisabled,
}: {
  name: string;
  size: number;
  hint?: string;
  onView?: () => void;
  onDownload?: () => void;
  onRemove?: () => void;
  /** Receives the full new name, extension included and unchanged. */
  onRename?: (name: string) => void;
  removeDisabled?: boolean;
}) {
  const { base, extension } = splitFileName(name);
  // The edit in progress, or null when the row is just showing its name. Seeded from the
  // current name each time editing starts, so a rename elsewhere is never overwritten by
  // a draft left over from before.
  const [draft, setDraft] = useState<string | null>(null);

  function commit(): void {
    const next = (draft ?? '').trim();
    if (!next) return;

    if (next !== base) onRename?.(`${next}${extension}`);
    setDraft(null);
  }

  if (draft !== null) {
    return (
      <div className="flex items-center gap-2 px-5 py-3">
        <Paperclip className="size-4 shrink-0 text-muted-foreground" />
        <div className="flex min-w-0 flex-1 items-center gap-1.5">
          <Input
            autoFocus
            value={draft}
            maxLength={MAX_BASE_NAME_LENGTH}
            aria-label={`Rename ${name}`}
            onChange={(event) => setDraft(sanitizeBaseName(event.target.value))}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                // The row can sit inside the assignment form; renaming a file is not
                // submitting it.
                event.preventDefault();
                commit();
              } else if (event.key === 'Escape') {
                setDraft(null);
              }
            }}
          />
          {/* Fixed, and deliberately outside the field — the file type is not the
              uploader's to change. */}
          {extension && (
            <span className="shrink-0 text-sm text-muted-foreground">{extension}</span>
          )}
        </div>
        <Button
          size="icon"
          variant="ghost"
          disabled={!draft.trim()}
          onClick={commit}
          aria-label="Save name"
        >
          <Check className="size-4" />
        </Button>
        <Button size="icon" variant="ghost" onClick={() => setDraft(null)} aria-label="Cancel rename">
          <X className="size-4" />
        </Button>
      </div>
    );
  }

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
      {onRename && (
        <Button
          size="icon"
          variant="ghost"
          onClick={() => setDraft(base)}
          aria-label={`Rename ${name}`}
        >
          <Pencil className="size-4" />
        </Button>
      )}
      {onView && (
        <Button size="icon" variant="ghost" onClick={onView} aria-label={`View ${name}`}>
          <Eye className="size-4" />
        </Button>
      )}
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
