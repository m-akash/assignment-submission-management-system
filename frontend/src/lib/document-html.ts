/**
 * Reducing converted-document markup to something this app is willing to inject.
 *
 * A `.docx` has no inline view a browser can give it, so its contents are converted to
 * HTML in the browser (see `file-preview.tsx`) and that HTML is handed to
 * `dangerouslySetInnerHTML`. Unlike the stored description and answer fields — whose
 * markup the server has already reduced to an allowlist, which is what
 * `isRenderableHtml` then re-checks — this markup is derived from a file a student
 * or teacher uploaded, has never been near the server's allowlist, and is produced by a
 * converter whose exact output is not this app's to guarantee. So it is sanitized here.
 *
 * This one *repairs* rather than refuses, which is the opposite of `rich-text.ts`, and for
 * a reason: a stored answer that fails a check still has a safe rendering (its own text),
 * whereas refusing a converted document outright would mean a legitimate brief with one
 * unexpected tag in it shows nothing at all. So the document is parsed, walked, and
 * everything outside the allowlist is dropped or unwrapped, leaving the text intact.
 *
 * Parsing happens in a `DOMParser` document, which has no browsing context: nothing in it
 * runs, and no `src` in it is fetched. The nodes are only ever serialised back to a string
 * — they are never adopted into the live document.
 */

/**
 * What a converted document is allowed to contain. Wider than the editor's allowlist,
 * because a real document has headings, tables and figures the editor cannot produce.
 */
const ALLOWED_TAGS = new Set([
  'p', 'br', 'hr', 'strong', 'b', 'em', 'i', 'u', 's', 'strike', 'sub', 'sup', 'ins', 'del',
  'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'ul', 'ol', 'li', 'dl', 'dt', 'dd',
  'a', 'img', 'table', 'caption', 'thead', 'tbody', 'tfoot', 'tr', 'th', 'td',
  'blockquote', 'pre', 'code',
]);

/**
 * Dropped with everything inside them. These carry no document text, so unwrapping them
 * — the treatment every other unrecognised tag gets — would keep contents that only exist
 * to be executed or fetched.
 */
const DISCARDED_TAGS = new Set([
  'script', 'style', 'noscript', 'template', 'iframe', 'frame', 'frameset', 'object',
  'embed', 'applet', 'svg', 'math', 'link', 'meta', 'base', 'form', 'input', 'select',
  'textarea', 'button', 'audio', 'video', 'source', 'track',
]);

/** Kept on any element: footnote and endnote links point at ids the converter wrote. */
const GLOBAL_ATTRIBUTES = new Set(['id']);

/** Everything else is per-tag, and nothing carries a style, a class or an event. */
const TAG_ATTRIBUTES: Record<string, Set<string>> = {
  a: new Set(['href']),
  img: new Set(['src', 'alt']),
  td: new Set(['colspan', 'rowspan']),
  th: new Set(['colspan', 'rowspan']),
};

/** A link out, an address, or one of the document's own footnote anchors. */
const SAFE_HREF = /^(?:https?:\/\/|mailto:|#)/i;

/**
 * An image the converter inlined from the document's own bytes. Only the raster types are
 * listed: an `image/svg+xml` data URI is a document that can carry script, and although an
 * `<img>` will not run it, there is no reason to accept one here.
 */
const SAFE_IMAGE_SRC = /^data:image\/(?:png|jpeg|gif|webp|bmp);base64,[A-Za-z0-9+/=\s]+$/i;

/**
 * Strips converted markup down to {@link ALLOWED_TAGS} and their permitted attributes.
 * Unrecognised elements are unwrapped so their text survives; the list above is removed
 * outright. Browser-only — it needs `DOMParser`.
 */
export function sanitizeDocumentHtml(html: string): string {
  if (!html) return '';

  const parsed = new DOMParser().parseFromString(html, 'text/html');
  sanitizeChildren(parsed.body);

  return parsed.body.innerHTML;
}

function sanitizeChildren(parent: Element): void {
  // A live child list shifts under the removals below, so the copy is what is walked.
  for (const node of Array.from(parent.childNodes)) {
    if (node.nodeType === Node.TEXT_NODE) continue;

    // Comments, CDATA and processing instructions: nothing to show, nothing to keep.
    if (node.nodeType !== Node.ELEMENT_NODE) {
      (node as ChildNode).remove();
      continue;
    }

    const element = node as Element;
    const tag = element.tagName.toLowerCase();

    if (DISCARDED_TAGS.has(tag)) {
      element.remove();
      continue;
    }

    // Depth first: an allowed element must not be left holding a disallowed one, and a
    // tag about to be unwrapped has to hand up children that are already clean.
    sanitizeChildren(element);

    if (!ALLOWED_TAGS.has(tag)) {
      unwrap(element);
      continue;
    }

    sanitizeAttributes(element, tag);
  }
}

function sanitizeAttributes(element: Element, tag: string): void {
  const allowed = TAG_ATTRIBUTES[tag];

  for (const attribute of Array.from(element.attributes)) {
    const name = attribute.name.toLowerCase();
    if (!GLOBAL_ATTRIBUTES.has(name) && !allowed?.has(name)) {
      element.removeAttribute(attribute.name);
    }
  }

  if (tag === 'a') {
    const href = element.getAttribute('href')?.trim() ?? '';

    if (!SAFE_HREF.test(href)) {
      // The text stays and stops being a link, which is how a `javascript:` target in
      // someone's document ends up inert rather than absent.
      element.removeAttribute('href');
    } else if (!href.startsWith('#')) {
      // Following a link out of a document opens a tab with no handle on the one it
      // came from. Fragment links stay in place: they point inside the preview.
      element.setAttribute('target', '_blank');
      element.setAttribute('rel', 'noopener noreferrer');
    }
  }

  // An image whose bytes did not come from the document itself would be a request this
  // preview has no reason to make, and one with no source at all is a broken icon.
  if (tag === 'img' && !SAFE_IMAGE_SRC.test(element.getAttribute('src')?.trim() ?? '')) {
    element.remove();
  }
}

/** Replaces an element with its children, keeping the text an unknown tag was wrapping. */
function unwrap(element: Element): void {
  const parent = element.parentNode;
  if (!parent) return;

  while (element.firstChild) parent.insertBefore(element.firstChild, element);
  parent.removeChild(element);
}
