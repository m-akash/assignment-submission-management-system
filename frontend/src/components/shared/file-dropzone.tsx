'use client';

import { useRef, useState } from 'react';
import { Loader2, Upload } from 'lucide-react';
import { toast } from 'sonner';
import { formatBytes } from '@/lib/format';
import { splitFileName } from '@/lib/file-name';
import { cn } from '@/lib/utils';

/**
 * Where files come into the app: drag a stack of them onto the panel, or click to browse.
 * Teachers attach material and students hand work back — the same gesture on both sides,
 * so it is one component rather than a pair that drift apart.
 *
 * What lands here is only ever *staged*. Nothing is sent until the surrounding screen says
 * so, which is what makes renaming possible at all: a picked file can still be corrected
 * (`IMG_20240817_113044.jpg` → `Question 3 working.jpg`) before it becomes an upload, and
 * a rename after the fact would mean the file is already filed under a name nobody meant.
 *
 * Every rule below is a mirror of the server's, not a substitute for it — the API re-checks
 * size, extension and the file's own signature on arrival. Checking here just means an
 * obvious mistake is caught in the panel instead of coming back as a failed request.
 */

/** UX-only mirror of `FileStorage:AllowedExtensions`. */
export const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx', '.txt', '.png', '.jpg', '.jpeg'];

/** UX-only mirror of `FileStorage:MaxBytes`. */
export const MAX_BYTES = 2 * 1024 * 1024;

export function FileDropzone({
  onFiles,
  remaining,
  busy,
  disabled,
  fullMessage = 'Attachment limit reached',
  accept = ALLOWED_EXTENSIONS,
  maxBytes = MAX_BYTES,
  className,
}: {
  /** The accepted files, in the order they were dropped or picked. Never called empty. */
  onFiles: (files: File[]) => void;
  /** How many more files this list has room for; at zero the zone reads as full. */
  remaining: number;
  /** An upload in flight — the zone stays visible but stops taking more. */
  busy?: boolean;
  disabled?: boolean;
  /** What to say once `remaining` hits zero, since the limit differs per screen. */
  fullMessage?: string;
  accept?: string[];
  maxBytes?: number;
  className?: string;
}) {
  const input = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);
  // Dragging over a child fires `dragleave` on the parent, so the highlight is held by a
  // depth count rather than a boolean the first inner element would switch off.
  const depth = useRef(0);

  const isFull = remaining <= 0;
  const isClosed = disabled || busy || isFull;

  /**
   * Filters a pick or a drop down to what may actually be attached, and says why for
   * each file it turns away — a drop of five where two are too big should not silently
   * become three.
   */
  function take(picked: FileList | null): void {
    const files = Array.from(picked ?? []);
    if (files.length === 0) return;

    const accepted: File[] = [];

    for (const file of files) {
      const { extension } = splitFileName(file.name);

      if (!accept.includes(extension.toLowerCase())) {
        toast.error(`${file.name} is not an allowed file type.`);
        continue;
      }

      // A dropped folder arrives as a zero-length entry; so does an empty file, and the
      // server rejects both with the same reason.
      if (file.size === 0) {
        toast.error(`${file.name} is empty.`);
        continue;
      }

      if (file.size > maxBytes) {
        toast.error(`${file.name} is larger than ${formatBytes(maxBytes)}.`);
        continue;
      }

      accepted.push(file);
    }

    if (accepted.length > remaining) {
      toast.error(
        remaining === 0
          ? 'The attachment limit has been reached.'
          : `Only ${remaining} more file${remaining === 1 ? '' : 's'} can be attached.`,
      );
    }

    const fitting = accepted.slice(0, Math.max(remaining, 0));
    if (fitting.length > 0) onFiles(fitting);
  }

  return (
    <div className={className}>
      {/* Outside the button: a form control may not be nested inside one. */}
      <input
        ref={input}
        type="file"
        hidden
        multiple
        accept={accept.join(',')}
        onChange={(event) => {
          take(event.target.files);
          // Cleared so picking the same file twice in a row still fires a change.
          event.target.value = '';
        }}
      />

      <button
        type="button"
        disabled={isClosed}
        onClick={() => input.current?.click()}
        onDragEnter={(event) => {
          event.preventDefault();
          depth.current += 1;
          if (!isClosed) setDragging(true);
        }}
        onDragOver={(event) => {
          // Without this the browser navigates to the file instead of letting it drop.
          event.preventDefault();
          event.dataTransfer.dropEffect = isClosed ? 'none' : 'copy';
        }}
        onDragLeave={() => {
          depth.current -= 1;
          if (depth.current <= 0) {
            depth.current = 0;
            setDragging(false);
          }
        }}
        onDrop={(event) => {
          event.preventDefault();
          depth.current = 0;
          setDragging(false);
          if (isClosed) return;
          take(event.dataTransfer.files);
        }}
        className={cn(
          'flex w-full flex-col items-center gap-1.5 rounded-xl border border-dashed px-4 py-6 text-center transition-colors outline-none',
          'focus-visible:ring-3 focus-visible:ring-ring/50',
          dragging
            ? 'border-primary bg-primary/10'
            : 'border-input hover:border-primary/60 hover:bg-muted/50',
          isClosed && 'cursor-not-allowed opacity-60 hover:border-input hover:bg-transparent',
        )}
      >
        <span
          className={cn(
            'flex size-9 items-center justify-center rounded-full bg-muted text-muted-foreground transition-colors',
            dragging && 'bg-primary/15 text-primary',
          )}
        >
          {busy ? <Loader2 className="size-4 animate-spin" /> : <Upload className="size-4" />}
        </span>

        <span className="text-sm font-medium">
          {isFull
            ? fullMessage
            : busy
              ? 'Uploading…'
              : dragging
                ? 'Drop to attach'
                : 'Drag files here, or click to browse'}
        </span>

        <span className="text-xs text-muted-foreground">
          {isFull
            ? 'Remove a file to attach another.'
            : `${accept.join(', ')} · up to ${formatBytes(maxBytes)} each · ${remaining} slot${remaining === 1 ? '' : 's'} left`}
        </span>
      </button>
    </div>
  );
}
