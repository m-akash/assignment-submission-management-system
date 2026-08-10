'use client';

import { useEffect, useRef, useState } from 'react';
import { Download, ImageOff, Loader2, Maximize2 } from 'lucide-react';
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
import { formatBytes } from '@/lib/format';

/**
 * Viewing an attachment without leaving the page — for the ones a browser can actually
 * render, which of the allowed upload types means images and nothing else. A PDF or a
 * .docx has no honest inline view, so those keep download as their only action.
 *
 * The bytes cannot be put in an `<img src>` directly: attachments are streamed by the API
 * behind an authorization check, so there is no URL a bare image request could use. The
 * blob is fetched the same way a download is and handed to the browser as an object URL,
 * which is revoked as soon as the dialog closes.
 *
 * Double-clicking the image takes it fullscreen through the browser's own Fullscreen API
 * rather than a wider dialog, so a photographed worksheet can be read at the size of the
 * screen — and Escape brings it back, as it would anywhere else.
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

/** The image extensions in `FileStorage:AllowedExtensions`. */
const IMAGE_EXTENSIONS = ['.png', '.jpg', '.jpeg'];

/**
 * Whether this attachment can be shown inline.
 *
 * The stored content type is derived server-side from the validated extension, so it is
 * the answer for anything uploaded through the current policy. The extension is checked
 * as well for rows written before that — a legacy `application/octet-stream` image should
 * still open rather than silently offering only a download.
 */
export function isViewableImage(contentType: string, fileName: string): boolean {
  if (contentType?.startsWith('image/')) return true;

  const dot = fileName.lastIndexOf('.');
  return dot > -1 && IMAGE_EXTENSIONS.includes(fileName.slice(dot).toLowerCase());
}

export function ImagePreviewDialog({
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
  // Both results remember which file they belong to, so opening a second image shows the
  // spinner again instead of the first one's bytes — and neither has to be cleared as the
  // effect starts, which would be a render cascade.
  const [loaded, setLoaded] = useState<{ id: string; url: string } | null>(null);
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

  useEffect(() => {
    if (!fileId) return;

    let objectUrl: string | null = null;
    let cancelled = false;

    load
      .current(fileId)
      .then((blob) => {
        // Nothing to show if the dialog closed, or moved on to another file, mid-flight.
        if (cancelled) return;
        objectUrl = URL.createObjectURL(blob);
        setLoaded({ id: fileId, url: objectUrl });
      })
      .catch((reason: unknown) => {
        if (cancelled) return;
        setFailed({
          id: fileId,
          message: reason instanceof Error ? reason.message : 'Could not load this image.',
        });
      });

    return () => {
      cancelled = true;
      // The object URL lives exactly as long as the dialog showing it.
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [fileId]);

  const url = loaded && loaded.id === fileId ? loaded.url : null;
  const error = failed && failed.id === fileId ? failed.message : null;

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
      // rather than letting a double-click do nothing.
      toast.error('This browser cannot show an image fullscreen. Download it instead.');
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
        className="sm:max-w-3xl"
        // While fullscreen, Escape belongs to the browser: it should bring the image
        // back into the dialog, not close the dialog underneath it.
        onEscapeKeyDown={(event) => {
          if (fullscreenElement()) event.preventDefault();
        }}
      >
        <DialogHeader>
          <DialogTitle className="truncate pr-8">{file?.name}</DialogTitle>
          <DialogDescription>
            {file ? formatBytes(file.sizeBytes) : null}
            {url && ' · double-click the image for fullscreen'}
          </DialogDescription>
        </DialogHeader>

        {/* Fixed height across all three states so the dialog does not jump as the
            bytes arrive, and a portrait image is not stretched to fill it. The frame,
            not the image, is what goes fullscreen — see `.preview-frame` in
            globals.css, which drops its border and goes dark at that size. */}
        <div
          ref={frame}
          className="preview-frame flex h-[60vh] items-center justify-center overflow-auto rounded-lg border bg-muted/30"
        >
          {error ? (
            <div className="flex flex-col items-center gap-2 p-6 text-center">
              <ImageOff className="size-6 text-muted-foreground" />
              <p className="text-sm text-muted-foreground">{error}</p>
              <p className="text-xs text-muted-foreground">
                You can still download the file instead.
              </p>
            </div>
          ) : url ? (
            /* eslint-disable-next-line @next/next/no-img-element -- an object URL for
               bytes already in memory: there is nothing for next/image to optimise. */
            <img
              src={url}
              alt={file?.name ?? ''}
              className="max-h-full max-w-full cursor-zoom-in object-contain"
              onDoubleClick={toggleFullscreen}
            />
          ) : (
            <Loader2 className="size-5 animate-spin text-muted-foreground" />
          )}
        </div>

        <DialogFooter showCloseButton>
          {url && (
            <Button variant="outline" onClick={toggleFullscreen}>
              <Maximize2 className="size-4" />
              Fullscreen
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
