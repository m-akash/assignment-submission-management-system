/**
 * Helpers for the two fields in the app that hold formatted text: an assignment's
 * description and a student's written answer, both authored in the Tiptap editor and
 * stored as HTML the server has run through its allowlist.
 *
 * These columns are not uniformly HTML, which is what the functions below exist for.
 * They held plain text before the editor landed and those rows were not migrated — a
 * brief written last term is still a bare string with newlines in it — so every read
 * path has to decide "markup or typing?" before it can render.
 *
 * {@link isRenderableHtml} is that decision, and it is deliberately stricter than
 * "does this contain a tag". Values written before the sanitizer existed never passed
 * through it, so a student who typed `<img onerror=…>` into the old plain textarea has
 * that string sitting in the database today. Answering "yes, markup" on the strength of
 * a `<` would hand it straight to `dangerouslySetInnerHTML`. Instead the value has to
 * consist only of what the editor and the server's allowlist can produce; anything else
 * is treated as text and escaped, which is exactly how it rendered before.
 *
 * This is a check, not a sanitizer — it never repairs a value, only refuses it — and
 * the fallback is always safe, so the two cannot disagree in a dangerous direction.
 */

/** The server's allowlist (HtmlContent.AllowedTags), mirrored. */
const ALLOWED_TAGS = new Set([
  'p', 'br', 'strong', 'b', 'em', 'i', 'u', 's', 'code',
  'h2', 'h3', 'ul', 'ol', 'li', 'blockquote', 'a',
]);

/** The only attributes the allowlist keeps, all of them on links. */
const ALLOWED_ATTRIBUTES = new Set(['href', 'target', 'rel']);

const SAFE_HREF = /^(?:https?:|mailto:)/i;

/** A tag, with quoted attribute values kept whole so a `>` inside one does not end it. */
const TAG = /<\/?([a-z][a-z0-9]*)((?:"[^"]*"|'[^']*'|[^>"'])*)>/gi;

/** An attribute name, and its value when it has one. */
const ATTRIBUTE = /([a-z_:][-\w:.]*)\s*(?:=\s*("[^"]*"|'[^']*'|[^\s>]*))?/gi;

/** Anything the tag pattern does not cover: comments, CDATA, processing instructions. */
const NON_TAG_MARKUP = /<[!?]/;

/** An opening or closing tag — enough to tell markup from a typed paragraph. */
const TAG_PATTERN = /<\/?[a-z][a-z0-9]*(\s[^>]*)?>/i;

/** Block-level boundaries; each becomes a line break when flattening to text. */
const BLOCK_BOUNDARY = /<\s*\/?\s*(br|p|div|li|ul|ol|h[1-6]|blockquote|pre|tr)\b[^>]*>/gi;

const ENTITIES: Record<string, string> = {
  '&nbsp;': ' ',
  '&amp;': '&',
  '&lt;': '<',
  '&gt;': '>',
  '&quot;': '"',
  '&apos;': "'",
};

/**
 * Whether a value may be rendered as markup: it contains tags, and every one of them —
 * with every attribute and every link target — is something the editor and the server's
 * allowlist can produce. False for plain text and false for anything unrecognised, both
 * of which the caller renders as escaped text instead.
 */
export function isRenderableHtml(value: string): boolean {
  if (!value || NON_TAG_MARKUP.test(value)) return false;

  let sawTag = false;

  for (const tag of value.matchAll(TAG)) {
    sawTag = true;
    if (!ALLOWED_TAGS.has(tag[1].toLowerCase())) return false;

    for (const attribute of tag[2].matchAll(ATTRIBUTE)) {
      const name = attribute[1].toLowerCase();
      if (!ALLOWED_ATTRIBUTES.has(name)) return false;

      // `javascript:` and `data:` are how a permitted tag still runs code.
      if (name === 'href' && !SAFE_HREF.test(unquote(attribute[2]))) return false;
    }
  }

  return sawTag;
}

/**
 * Flatten a value to plain text — for excerpts, previews and length checks, anywhere the
 * formatting cannot be shown. Legacy plain text passes through as-is. Unlike the check
 * above this runs over anything, because stripping tags is safe whatever they were.
 */
export function richTextToPlainText(value: string): string {
  if (!value) return '';
  if (!TAG_PATTERN.test(value)) return value.trim();

  const withBreaks = value.replace(BLOCK_BOUNDARY, '\n');
  const stripped = withBreaks.replace(/<[^>]*>/g, '');

  return decodeEntities(stripped)
    // Horizontal whitespace only — the breaks above carry the paragraph structure.
    .replace(/[^\S\n]+/g, ' ')
    .replace(/ ?\n ?/g, '\n')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

/**
 * Whether a value carries no words. The editor never yields an empty string — an emptied
 * one serialises to `<p></p>` — so "required" cannot be a length check on the raw value
 * and has to look at the text inside.
 */
export function isRichTextEmpty(value: string): boolean {
  return richTextToPlainText(value).length === 0;
}

/**
 * Prepare a stored value for loading into the editor. Anything that is not renderable
 * markup is promoted to escaped paragraphs, which covers both halves of the problem:
 * plain text keeps line breaks the HTML parser would otherwise collapse, and a hostile
 * legacy value is never parsed into DOM nodes that could fire on their own.
 */
export function toEditorHtml(value: string): string {
  if (!value) return '';
  if (isRenderableHtml(value)) return value;

  return value
    .split(/\n{2,}/)
    .map((block) => `<p>${escapeHtml(block).replace(/\n/g, '<br>')}</p>`)
    .join('');
}

function unquote(value: string | undefined): string {
  return (value ?? '').replace(/^["']|["']$/g, '').trim();
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function decodeEntities(value: string): string {
  return value
    .replace(/&#(\d+);/g, (_, code: string) => String.fromCodePoint(Number(code)))
    .replace(/&[a-z]+;/gi, (entity) => ENTITIES[entity.toLowerCase()] ?? entity);
}
