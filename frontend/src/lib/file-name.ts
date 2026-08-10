/**
 * A file's name, in the two parts a person thinks of separately: what they called it,
 * and what kind of file it is.
 *
 * Only the first half is ever editable in this app. The extension is what the server
 * validates the bytes against — it reads the file's signature and refuses anything whose
 * contents disagree with its extension — so letting someone edit it could only produce a
 * lie or a rejected upload. Renaming is about the label, not the format.
 */

/** Characters no filesystem accepts; the server replaces this same set with underscores. */
const INVALID_CHARACTERS = '\\/:*?"<>|';

/**
 * The server truncates a stored name at 255 characters. Bounding the editable half well
 * below that leaves room for the extension and keeps a pasted essay out of the field.
 */
export const MAX_BASE_NAME_LENGTH = 200;

export interface SplitName {
  /** The name without its extension — "notes" in "notes.pdf". */
  base: string;
  /** The extension including its dot, or an empty string — ".pdf" in "notes.pdf". */
  extension: string;
}

/**
 * Splits at the *last* dot, so "report.v2.pdf" keeps "report.v2" as its base. A leading
 * dot is a dotfile's whole name rather than an extension, so it stays in the base.
 */
export function splitFileName(name: string): SplitName {
  const dot = name.lastIndexOf('.');
  if (dot <= 0) return { base: name, extension: '' };

  return { base: name.slice(0, dot), extension: name.slice(dot) };
}

/**
 * What a typed name is allowed to contain: no path separators, no control characters,
 * nothing a filesystem would reject. Applied as the field changes rather than on save, so
 * the row shows exactly what will be stored — the server sanitizes the same characters,
 * and having it silently rewrite the name afterwards would surprise the uploader.
 */
export function sanitizeBaseName(value: string): string {
  let cleaned = '';
  for (const character of value) {
    // Everything below the space is a control character.
    if (character < ' ' || INVALID_CHARACTERS.includes(character)) continue;
    cleaned += character;
  }

  return cleaned.slice(0, MAX_BASE_NAME_LENGTH);
}

/**
 * The same bytes under a new name.
 *
 * The original extension is re-attached whatever the caller passes, which is the one rule
 * renaming has: a `.pdf` stays a `.pdf`. An empty or unchanged name returns the file
 * untouched, so a caller can hand this every edit without checking first.
 *
 * A `File` is immutable, hence the copy — it shares the underlying blob, so this costs
 * nothing in memory. The new name is what `FormData` sends as the upload's filename, and
 * what the server stores as `OriginalFileName`.
 */
export function renameFile(file: File, name: string): File {
  const { extension } = splitFileName(file.name);
  const base = sanitizeBaseName(splitFileName(name).base).trim();
  if (!base) return file;

  const next = `${base}${extension}`;
  if (next === file.name) return file;

  return new File([file], next, { type: file.type, lastModified: file.lastModified });
}
