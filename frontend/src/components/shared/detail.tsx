'use client';

import { useState } from 'react';
import {
  Check,
  Download,
  Eye,
  FileText,
  ImageIcon,
  type LucideIcon,
  Paperclip,
  Pencil,
  Trash2,
  X,
} from 'lucide-react';
import {
  Attachment,
  AttachmentAction,
  AttachmentActions,
  AttachmentContent,
  AttachmentDescription,
  AttachmentMedia,
  AttachmentTitle,
} from '@/components/ui/attachment';
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
 * The same facts as a `Fact` rail, laid along one line under the title instead of stacked
 * down the side: the record's state and figures first, then the actions that change them.
 * A handful of short values reads faster across than down, and the width it gives back
 * goes to the work itself.
 */
export function MetaBar({
  children,
  actions,
}: {
  children: React.ReactNode;
  /** Pushed to the end of the same line, so state and the buttons acting on it sit together. */
  actions?: React.ReactNode;
}) {
  return (
    <div className="panel flex flex-wrap items-center gap-x-5 gap-y-3 px-4 py-3">
      {children}
      {actions && <div className="ml-auto flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}

/** One label/value pair on a `MetaBar`, with an icon to find it by. */
export function MetaItem({
  icon: Icon,
  label,
  children,
}: {
  icon: LucideIcon;
  label: string;
  children: React.ReactNode;
}) {
  return (
    <span className="flex items-center gap-1.5 text-sm whitespace-nowrap">
      <Icon className="size-3.5 shrink-0 text-muted-foreground" />
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium">{children}</span>
    </span>
  );
}

/** A hairline between groups of items on a `MetaBar` — badges from figures, say. */
export function MetaDivider() {
  return <span aria-hidden className="hidden h-5 w-px bg-border sm:block" />;
}

/** The allowed upload types, as icons — an image is recognisable before its name is read. */
const IMAGE_EXTENSIONS = ['.png', '.jpg', '.jpeg'];
const DOCUMENT_EXTENSIONS = ['.pdf', '.txt', '.doc', '.docx'];

/** An element rather than a component: picking a component per render would reset it. */
function fileIcon(extension: string): React.ReactNode {
  const suffix = extension.toLowerCase();
  if (IMAGE_EXTENSIONS.includes(suffix)) return <ImageIcon />;
  if (DOCUMENT_EXTENSIONS.includes(suffix)) return <FileText />;
  return <Paperclip />;
}

/**
 * A file in a list: name, size, and whichever of view/download/remove/rename the caller
 * allows. Teacher materials and student attachments both use it, so both read the same way.
 *
 * Built on shadcn's `Attachment`, so a file looks the same wherever it appears and the
 * states it can be in are the component's own: `done` for something the server holds,
 * `idle` — an outline rather than a filled card — for a pick that is still only staged.
 *
 * `onView` is passed only for files that have an inline view — everything except the
 * legacy binary `.doc`, in practice; see `canPreview` in `file-preview.tsx`. A file
 * without one keeps download as its single action rather than opening a dialog that
 * would have nothing to put in it.
 *
 * `onRename` is passed only where a correction is still possible. It edits the name and
 * nothing else: the extension is shown beside the field but never inside it, because it
 * describes the bytes rather than labelling them.
 */
export function FileRow({
  name,
  size,
  hint,
  pending,
  onView,
  onDownload,
  onRemove,
  onRename,
  removeDisabled,
}: {
  name: string;
  size: number;
  hint?: string;
  /** Staged in the browser and not sent yet — drawn as an outline, not a filled card. */
  pending?: boolean;
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

  const icon = fileIcon(extension);
  const state = pending ? 'idle' : 'done';

  if (draft !== null) {
    return (
      <Attachment state={state} className="w-full">
        <AttachmentMedia>{icon}</AttachmentMedia>
        <AttachmentContent className="flex items-center gap-1.5">
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
        </AttachmentContent>
        <AttachmentActions>
          <AttachmentAction disabled={!draft.trim()} onClick={commit} aria-label="Save name">
            <Check />
          </AttachmentAction>
          <AttachmentAction onClick={() => setDraft(null)} aria-label="Cancel rename">
            <X />
          </AttachmentAction>
        </AttachmentActions>
      </Attachment>
    );
  }

  return (
    <Attachment state={state} className="w-full">
      <AttachmentMedia>{icon}</AttachmentMedia>
      <AttachmentContent>
        <AttachmentTitle>{name}</AttachmentTitle>
        <AttachmentDescription>
          {formatBytes(size)}
          {hint && ` · ${hint}`}
        </AttachmentDescription>
      </AttachmentContent>
      <AttachmentActions>
        {onRename && (
          <AttachmentAction onClick={() => setDraft(base)} aria-label={`Rename ${name}`}>
            <Pencil />
          </AttachmentAction>
        )}
        {onView && (
          <AttachmentAction onClick={onView} aria-label={`View ${name}`}>
            <Eye />
          </AttachmentAction>
        )}
        {onDownload && (
          <AttachmentAction onClick={onDownload} aria-label={`Download ${name}`}>
            <Download />
          </AttachmentAction>
        )}
        {onRemove && (
          <AttachmentAction
            disabled={removeDisabled}
            onClick={onRemove}
            aria-label={`Remove ${name}`}
            className="text-danger hover:text-danger"
          >
            <Trash2 />
          </AttachmentAction>
        )}
      </AttachmentActions>
    </Attachment>
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
