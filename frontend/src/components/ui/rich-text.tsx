import { cn } from '@/lib/utils';
import { isRenderableHtml } from '@/lib/rich-text';

/**
 * Renders a stored description or answer — the read-side counterpart to `RichTextEditor`,
 * sharing its `rich-text` prose styles so a brief looks the same being written and being
 * read.
 *
 * The HTML branch injects markup, which is safe because of the two gates in front of it:
 * the server reduces every write to an allowlist, and `isRenderableHtml` refuses anything
 * that is not already within it — which is what covers rows written before that allowlist
 * existed. A value that fails either takes the text branch and is escaped by React, which
 * is also how legacy plain text keeps its newlines.
 */
export function RichText({
  content,
  className,
}: {
  content: string;
  className?: string;
}) {
  if (!content) return null;

  if (!isRenderableHtml(content)) {
    return <p className={cn('text-sm leading-relaxed whitespace-pre-wrap', className)}>{content}</p>;
  }

  return (
    <div
      className={cn('rich-text', className)}
      dangerouslySetInnerHTML={{ __html: content }}
    />
  );
}
