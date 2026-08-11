'use client';

import { useEffect, useRef, useState } from 'react';
import { Download, FileX, Loader2, Maximize2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { sanitizeDocumentHtml } from '@/lib/document-html';
import { formatBytes } from '@/lib/format';
import { cn } from '@/lib/utils';

/**
 * Viewing an attachment without leaving the page. Every allowed upload type except the
 * legacy binary `.doc` opens here — an image, a PDF, a text file, or a `.docx`.
 *
 * The bytes cannot be put in a `src` directly: attachments are streamed by the API behind
 * an authorization check, so there is no URL a bare image or frame request could use. The
 * blob is fetched the same way a download is, and each kind is then prepared for the one
 * thing that can show it:
 *
 * - an image becomes an object URL behind an `<img>`;
 * - a PDF becomes an object URL in an `<iframe>`, which hands it to the browser's own
 *   viewer — page navigation, zoom and search included, and no library to ship;
 * - a text file is decoded and printed as text, never as markup;
 * - a `.docx` has no native view anywhere, so it is converted to HTML in the browser and
 *   sanitized before being rendered. The conversion keeps text, headings, lists, tables
 *   and inline images; it does not reproduce page layout, and is not meant to — the
 *   download is still there for anyone who needs the document exactly as it was written.
 *
 * Fullscreen goes through the browser's own Fullscreen API rather than a wider dialog, so
 * a photographed worksheet or a dense PDF can be read at the size of the screen — and
 * Escape brings it back, as it would anywhere else.
 */

/**
 * The prefixed half of the Fullscreen API, which older WebKit is the only one to need.
 * Declared rather than cast around, so the calls below stay readable.
 */
declare global {
  interface Element {
    webkitRequestFullscreen?: () => Promise<void> | void;
  }

  interface Document {
    webkitFullscreenElement?: Element | null;
    webkitExitFullscreen?: () => Promise<void> | void;
  }
}

/** Whatever is currently fullscreen, under either spelling. */
function fullscreenElement(): Element | null {
  return document.fullscreenElement ?? document.webkitFullscreenElement ?? null;
}

function exitFullscreen(): void {
  if (!fullscreenElement()) return;

  if (typeof document.exitFullscreen === 'function') {
    void Promise.resolve(document.exitFullscreen()).catch(() => {});
  } else if (typeof document.webkitExitFullscreen === 'function') {
    void Promise.resolve(document.webkitExitFullscreen()).catch(() => {});
  }
}

/** One attachment, flattened from whichever DTO the caller holds. */
export interface PreviewFile {
  id: string;
  name: string;
  contentType: string;
  sizeBytes: number;
}

/** How a file is shown, which is also what decides how its bytes are prepared. */
export type PreviewKind = 'image' | 'pdf' | 'text' | 'docx';

/**
 * The extensions in `FileStorage:AllowedExtensions` that have an inline view. `.doc` is
 * absent on purpose: the legacy binary format has no viewer in any browser and no honest
 * client-side conversion, so it keeps download as its only action.
 */
const KIND_BY_EXTENSION: Record<string, PreviewKind> = {
  '.png': 'image',
  '.jpg': 'image',
  '.jpeg': 'image',
  '.pdf': 'pdf',
  '.txt': 'text',
  '.docx': 'docx',
};

const DOCX_CONTENT_TYPE =
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document';

/**
 * How this attachment can be shown, or `null` for one that cannot be.
 *
 * The extension is the answer for anything uploaded through the current policy, because
 * the server validates it against the file's own signature and derives the stored content
 * type from it. The type is consulted only when the name carries no extension at all,
 * which is how a row written before that policy still opens rather than silently offering
 * a download alone.
 */
export function previewKind(contentType: string, fileName: string): PreviewKind | null {
  const dot = fileName.lastIndexOf('.');
  if (dot > -1) {
    const kind = KIND_BY_EXTENSION[fileName.slice(dot).toLowerCase()];
    if (kind) return kind;
  }

  if (contentType?.startsWith('image/')) return 'image';
  if (contentType === 'application/pdf') return 'pdf';
  if (contentType?.startsWith('text/plain')) return 'text';
  if (contentType === DOCX_CONTENT_TYPE) return 'docx';

  return null;
}

/** Whether to offer a view action at all — the shape a file list actually asks for. */
export function canPreview(contentType: string, fileName: string): boolean {
  return previewKind(contentType, fileName) !== null;
}

/** A file's bytes, turned into the one thing that can render them. */
type PreviewContent =
  | { kind: 'image' | 'pdf'; url: string }
  | { kind: 'text'; text: string }
  | { kind: 'docx'; html: string };

async function prepare(kind: PreviewKind, blob: Blob): Promise<PreviewContent> {
  switch (kind) {
    case 'image':
      return { kind, url: URL.createObjectURL(blob) };

    case 'pdf':
      // The blob's type is what decides whether the browser shows this in its viewer or
      // offers to save it, and the download response's own type is not something a legacy
      // row can be trusted for — so it is stated here rather than inherited.
      return { kind, url: URL.createObjectURL(new Blob([blob], { type: 'application/pdf' })) };

    case 'text':
      return { kind, text: await blob.text() };

    case 'docx': {
      // Loaded only when a document is actually opened: the converter is an order of
      // magnitude larger than this dialog, and most previews never need it. It is a
      // CommonJS module behind `export =`, so it arrives on `default` by way of the
      // bundler's interop — with the namespace itself as the fallback, which is what a
      // bundler that does not synthesize one would hand over.
      const imported = await import('mammoth');
      const mammoth = imported.default ?? (imported as unknown as typeof imported.default);

      const { value } = await mammoth.convertToHtml(
        { arrayBuffer: await blob.arrayBuffer() },
        {
          // Footnote anchors become element ids, so they are namespaced away from the
          // app's own; and nothing outside the file itself is ever read for it.
          idPrefix: 'docx-',
          externalFileAccess: false,
        },
      );

      return { kind, html: sanitizeDocumentHtml(value) };
    }
  }
}

function releaseContent(content: PreviewContent): void {
  if (content.kind === 'image' || content.kind === 'pdf') URL.revokeObjectURL(content.url);
}

/** Dialog width: a page of A4 or a converted document needs more room than a photo. */
const DIALOG_WIDTH: Record<PreviewKind, string> = {
  image: 'sm:max-w-3xl',
  pdf: 'sm:max-w-5xl',
  text: 'sm:max-w-3xl',
  docx: 'sm:max-w-4xl',
};

/**
 * Frame height by kind. Fixed per kind so the dialog does not jump as the bytes arrive,
 * and never so tall that the header and footer are pushed off the screen.
 *
 * A phone gets its own, shorter pair, in `dvh` rather than `vh`: `vh` on mobile is the
 * viewport with the browser's address bar pretended away, so a frame sized in it sits
 * partly underneath the bar until the user scrolls. `dvh` is the space actually visible.
 */
const FRAME_HEIGHT: Record<PreviewKind, string> = {
  image: 'h-[46dvh] sm:h-[60vh]',
  pdf: 'h-[56dvh] sm:h-[70vh]',
  text: 'h-[46dvh] sm:h-[60vh]',
  docx: 'h-[56dvh] sm:h-[70vh]',
};

/**
 * What the dialog costs around the frame — title, description, both gaps, the padding and
 * the footer — and so how much of the screen the frame may not take. Held as a ceiling on
 * the frame rather than a `max-height` on the dialog, because a grid whose middle row is
 * over-tall clips at both ends: the header goes off the top of the screen and the download
 * button off the bottom. It only bites where the preferred height above will not fit — a
 * phone held sideways, or a very short window — and the floor keeps the frame from
 * collapsing to a sliver on the way.
 */
const FRAME_BOUNDS = 'max-h-[calc(100dvh-13rem)] min-h-32';

export function FilePreviewDialog({
  file,
  loadBlob,
  onDownload,
  onClose,
}: {
  /** The file being viewed; `null` closes the dialog. */
  file: PreviewFile | null;
  /** Fetches the bytes — the same endpoint the download uses. */
  loadBlob: (fileId: string) => Promise<Blob>;
  onDownload: (file: PreviewFile) => void;
  onClose: () => void;
}) {
  // Both results remember which file they belong to, so opening a second file shows the
  // spinner again instead of the first one's contents. Neither is cleared as the effect
  // starts, which would be a render cascade; the effect's cleanup is what retires them.
  const [loaded, setLoaded] = useState<{ id: string; content: PreviewContent } | null>(null);
  const [failed, setFailed] = useState<{ id: string; message: string } | null>(null);
  const frame = useRef<HTMLDivElement>(null);

  // Held in a ref, not read as a dependency: callers pass an inline arrow, and a fetch
  // that re-ran on every render would loop forever off its own state update. Which file
  // to load is the only thing that should restart it.
  const load = useRef(loadBlob);
  useEffect(() => {
    load.current = loadBlob;
  });

  const fileId = file?.id;
  const kind = file ? previewKind(file.contentType, file.name) : null;

  useEffect(() => {
    if (!fileId || !kind) return;

    let objectUrl: string | null = null;
    let cancelled = false;

    load
      .current(fileId)
      .then((blob) => prepare(kind, blob))
      .then((content) => {
        // Nothing to show if the dialog closed, or moved on to another file, mid-flight —
        // but a URL created for it still has to be given back.
        if (cancelled) {
          releaseContent(content);
          return;
        }

        if (content.kind === 'image' || content.kind === 'pdf') objectUrl = content.url;
        setLoaded({ id: fileId, content });
      })
      .catch((reason: unknown) => {
        if (cancelled) return;
        setFailed({
          id: fileId,
          message: reason instanceof Error ? reason.message : 'Could not open this file.',
        });
      });

    return () => {
      cancelled = true;
      // An object URL lives exactly as long as the dialog showing it.
      if (objectUrl) URL.revokeObjectURL(objectUrl);
      // And the result goes with it. Reopening the same file has to load it again rather
      // than spend a frame pointing an `<img>` or a frame at a URL the browser has
      // already released — and a second attempt should show a spinner, not the error the
      // first one left behind.
      setLoaded((current) => (current?.id === fileId ? null : current));
      setFailed((current) => (current?.id === fileId ? null : current));
    };
  }, [fileId, kind]);

  const content = loaded && loaded.id === fileId ? loaded.content : null;
  const error = failed && failed.id === fileId ? failed.message : null;

  // A file that converted or decoded to nothing at all: not a failure, but there is
  // nothing to draw, and an empty frame would read as one that is still loading.
  const empty =
    (content?.kind === 'text' && content.text.trim().length === 0) ||
    (content?.kind === 'docx' && content.html.length === 0);

  /** Double-click, or the footer button: the same toggle either way. */
  function toggleFullscreen() {
    if (fullscreenElement()) {
      exitFullscreen();
      return;
    }

    const element = frame.current;
    if (!element) return;

    // A rejected request is the browser's decision (it only grants this off a user
    // gesture), not a failure worth a toast of its own.
    if (typeof element.requestFullscreen === 'function') {
      void Promise.resolve(element.requestFullscreen()).catch(() => {});
    } else if (typeof element.webkitRequestFullscreen === 'function') {
      // Older WebKit only has the prefixed call.
      void Promise.resolve(element.webkitRequestFullscreen()).catch(() => {});
    } else {
      // iOS Safari grants fullscreen to video and nothing else, so say so plainly
      // rather than letting the button do nothing.
      toast.error('This browser cannot show a file fullscreen. Download it instead.');
    }
  }

  return (
    <Dialog
      open={!!file}
      onOpenChange={(open) => {
        if (open) return;
        // Leaving the dialog fullscreen behind would strand the browser on a blank
        // viewport, so the two are closed together.
        exitFullscreen();
        onClose();
      }}
    >
      <DialogContent
        // A phone gives the file the screen: a narrower gutter than the dialog's own, and
        // the frame as the one row that gives way when there is not enough height.
        className={cn(
          'max-w-[calc(100%-1rem)] grid-rows-[auto_minmax(0,1fr)_auto]',
          kind ? DIALOG_WIDTH[kind] : 'sm:max-w-md',
        )}
        // While fullscreen, Escape belongs to the browser: it should bring the file
        // back into the dialog, not close the dialog underneath it.
        onEscapeKeyDown={(event) => {
          if (fullscreenElement()) event.preventDefault();
        }}
      >
        <DialogHeader>
          {/* A phone is narrow enough that one line of a real file name is mostly
              ellipsis, so it gets two; a wider dialog keeps the title to one. Either way
              the full name is on the element itself, for a pointer to rest on. */}
          {/* `leading-none` is a one-line title's setting; two lines of it collide. */}
          <DialogTitle
            className="pr-8 max-sm:line-clamp-2 max-sm:leading-snug sm:truncate"
            title={file?.name}
          >
            {file?.name}
          </DialogTitle>
          <DialogDescription>
            {file ? formatBytes(file.sizeBytes) : null}
            {kind === 'docx' && content && ' · converted for reading; layout is approximate'}
            {/* Only where there is a mouse to double-click with. A touch screen has the
                footer button, and a second tap there means zoom. */}
            {kind === 'image' && content && (
              <span className="max-sm:hidden"> · double-click the image for fullscreen</span>
            )}
          </DialogDescription>
        </DialogHeader>

        {/* The frame, not the file, is what goes fullscreen — see `.preview-frame` in
            globals.css, which drops its border at that size and grounds each kind the way
            it wants to be read. Content sits at the top rather than centred so that
            something taller than the frame scrolls instead of being clipped at both ends;
            the spinner and the messages centre themselves. */}
        <div
          ref={frame}
          data-kind={kind ?? undefined}
          className={cn(
            'preview-frame flex items-center justify-center overflow-auto rounded-lg border bg-muted/30',
            FRAME_BOUNDS,
            kind ? FRAME_HEIGHT[kind] : 'h-40',
          )}
        >
          {!kind ? (
            <FrameMessage
              message="This file type cannot be shown here."
              hint="Download it to open it in an app that can."
            />
          ) : error ? (
            <FrameMessage message={error} hint="You can still download the file instead." />
          ) : empty ? (
            <FrameMessage
              message={
                kind === 'docx'
                  ? 'This document has no text to show.'
                  : 'This file is empty.'
              }
              hint="Download it if you need the file itself."
            />
          ) : content?.kind === 'image' ? (
            /* eslint-disable-next-line @next/next/no-img-element -- an object URL for
               bytes already in memory: there is nothing for next/image to optimise. */
            <img
              src={content.url}
              alt={file?.name ?? ''}
              className="max-h-full max-w-full cursor-zoom-in object-contain"
              onDoubleClick={toggleFullscreen}
            />
          ) : content?.kind === 'pdf' ? (
            // The browser's viewer, which owns its own scrolling and keyboard — including
            // the double-click a mouse would otherwise spend on fullscreen, which is why
            // the footer button is the way in for a PDF.
            <iframe src={content.url} title={file?.name ?? 'PDF'} className="size-full border-0" />
          ) : content?.kind === 'text' ? (
            <pre className="w-full self-start p-3 font-mono text-xs leading-relaxed wrap-break-word whitespace-pre-wrap sm:p-4">
              {content.text}
            </pre>
          ) : content?.kind === 'docx' ? (
            <div
              className="rich-text document-preview w-full self-start p-4 sm:p-6"
              dangerouslySetInnerHTML={{ __html: content.html }}
            />
          ) : (
            <Loader2 className="size-5 animate-spin text-muted-foreground" />
          )}
        </div>

        {/* Kept on one line at every width. Stacked, three full-width buttons are a third
            of a phone screen spent on the actions rather than on the file. */}
        <DialogFooter showCloseButton className="flex-row justify-end">
          {content && !empty && (
            <Button variant="outline" onClick={toggleFullscreen}>
              <Maximize2 className="size-4" />
              {/* The icon says it on a narrow screen; the name is still there to be read
                  aloud. */}
              <span className="max-sm:sr-only">Fullscreen</span>
            </Button>
          )}
          {file && (
            <Button variant="outline" onClick={() => onDownload(file)}>
              <Download className="size-4" />
              Download
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Anything the frame has to say instead of showing a file: an error, or an empty one. */
function FrameMessage({ message, hint }: { message: string; hint: string }) {
  return (
    <div className="flex flex-col items-center gap-2 p-6 text-center">
      <FileX className="size-6 text-muted-foreground" />
      <p className="text-sm text-muted-foreground">{message}</p>
      <p className="text-xs text-muted-foreground">{hint}</p>
    </div>
  );
}
