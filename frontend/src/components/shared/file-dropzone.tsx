'use client';

import { useRef, useState, type DragEvent } from 'react';
import { Loader2, Upload } from 'lucide-react';
import { toast } from 'sonner';
import {
  Attachment,
  AttachmentContent,
  AttachmentDescription,
  AttachmentMedia,
  AttachmentTitle,
  AttachmentTrigger,
} from '@/components/ui/attachment';
import { formatBytes } from '@/lib/format';
import { splitFileName } from '@/lib/file-name';
import { cn } from '@/lib/utils';

/**
 * Where files come into the app: drag a stack of them onto the panel, or click to browse.
 * Teachers attach material and students hand work back — the same gesture on both sides,
 * so it is one component rather than a pair that drift apart.
 *
 * It comes in two layouts, because "where files come in" is a different size of thing on
 * either side of the app. In the student's narrow rail it is `row`: shadcn's `Attachment`
 * in its `idle` state — the outlined, not-yet-filled version of the very card each picked
 * file becomes — so the empty slot and the files it fills are visibly the same object.
 * Stretched across a teacher's full-width panel that same row reads as a thin bar with its
 * label stranded on the left, so wide callers ask for `panel`: the same dashed outline and
 * muted icon tile, laid out centred and given height, which is the shape a drop target is
 * expected to have when there is room for one.
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
  variant = 'row',
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
  /** `row` for a narrow column, `panel` for a full-width one. See the note above. */
  variant?: 'row' | 'panel';
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

  /** Shared by both layouts, so a drop behaves identically whichever shape it landed on. */
  const dragHandlers = {
    onDragEnter(event: DragEvent) {
      event.preventDefault();
      depth.current += 1;
      if (!isClosed) setDragging(true);
    },
    onDragOver(event: DragEvent) {
      // Without this the browser navigates to the file instead of letting it drop.
      event.preventDefault();
      event.dataTransfer.dropEffect = isClosed ? 'none' : 'copy';
    },
    onDragLeave() {
      depth.current -= 1;
      if (depth.current <= 0) {
        depth.current = 0;
        setDragging(false);
      }
    },
    onDrop(event: DragEvent) {
      event.preventDefault();
      depth.current = 0;
      setDragging(false);
      if (isClosed) return;
      take(event.dataTransfer.files);
    },
  };

  const title = isFull
    ? fullMessage
    : busy
      ? 'Uploading…'
      : dragging
        ? 'Drop to attach'
        : 'Drag files here, or click to browse';

  // `PDF, DOC, …` rather than `.pdf, .doc, …`: the dots are punctuation the reader has to
  // look past, and the list sits beside a size limit that is already prose.
  const kinds = accept.map((extension) => extension.replace('.', '').toUpperCase()).join(', ');
  const constraints = `${kinds} · up to ${formatBytes(maxBytes)} each`;
  const slots = `${remaining} slot${remaining === 1 ? '' : 's'} left`;

  const icon = busy ? <Loader2 className="animate-spin" /> : <Upload />;

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

      {variant === 'panel' ? (
        // Not `Attachment` here: its padding is applied through `has-[…]` selectors that
        // outweigh anything passed in, so a taller box has to own its own spacing. The
        // dashed outline and muted icon tile are repeated by hand to match it.
        <div
          {...dragHandlers}
          className={cn(
            'relative flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed bg-card px-6 py-8 text-center transition-colors focus-within:ring-1 focus-within:ring-ring/50',
            dragging && 'border-primary bg-primary/5',
            isClosed
              ? 'opacity-60'
              : 'cursor-pointer hover:border-muted-foreground/40 hover:bg-muted/40',
          )}
        >
          <div
            className={cn(
              'flex size-11 shrink-0 items-center justify-center rounded-xl bg-muted text-foreground transition-colors [&_svg]:size-5',
              dragging && 'bg-primary/15 text-primary',
            )}
          >
            {icon}
          </div>

          <div className="space-y-1">
            <p className={cn('text-sm font-medium', busy && 'shimmer')}>{title}</p>
            <p className="text-xs text-muted-foreground">
              {isFull ? 'Remove a file to attach another.' : constraints}
            </p>
            {/* Smaller rather than fainter — a count nobody can read is worse than none. */}
            {!isFull && <p className="text-[11px] text-muted-foreground">{slots}</p>}
          </div>

          {/* Covers the whole box, so the click target is the drop area rather than a
              button inside it — and stays a real button, so the keyboard reaches it. */}
          <button
            type="button"
            disabled={isClosed}
            aria-label="Attach files"
            onClick={() => input.current?.click()}
            className="absolute inset-0 rounded-xl outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
          />
        </div>
      ) : (
        <Attachment
          state={busy ? 'uploading' : 'idle'}
          {...dragHandlers}
          className={cn(
            'w-full transition-colors',
            dragging && 'border-primary bg-primary/10',
            isClosed ? 'opacity-60' : 'cursor-pointer hover:bg-muted/50',
          )}
        >
          <AttachmentMedia className={cn(dragging && 'bg-primary/15 text-primary')}>
            {icon}
          </AttachmentMedia>

          <AttachmentContent>
            <AttachmentTitle>{title}</AttachmentTitle>
            <AttachmentDescription className="overflow-visible whitespace-normal">
              {isFull ? 'Remove a file to attach another.' : `${constraints} · ${slots}`}
            </AttachmentDescription>
          </AttachmentContent>

          {/* Covers the whole card, so the click target is the panel rather than a button
              inside it — and stays a real button, so the keyboard reaches it. */}
          <AttachmentTrigger
            disabled={isClosed}
            aria-label="Attach files"
            onClick={() => input.current?.click()}
            className="rounded-xl focus-visible:ring-3 focus-visible:ring-ring/50"
          />
        </Attachment>
      )}
    </div>
  );
}
