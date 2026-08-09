'use client';

import { useEffect, useState } from 'react';
import { EditorContent, useEditor, useEditorState } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import { CharacterCount, Placeholder } from '@tiptap/extensions';
import {
  Bold,
  Check,
  Code,
  Heading2,
  Heading3,
  Italic,
  Link2,
  Link2Off,
  List,
  ListOrdered,
  Quote,
  Redo2,
  Strikethrough,
  Undo2,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import { toEditorHtml } from '@/lib/rich-text';

/**
 * How much text a description may hold, counted in characters the author typed
 * rather than in markup — so adding a list does not eat someone's budget. Enforced
 * here as a hard stop rather than a validation message, because a limit that only
 * appears after you press save is a limit you find out about too late.
 */
export const RICH_TEXT_LIMIT = 5000;

/**
 * The app's formatted-text field: a Tiptap editor styled as one of our inputs, with
 * a fixed toolbar above it. Controlled the same way a `<textarea>` is — `value` is
 * HTML in, `onChange` gives HTML out, and an emptied editor reports `''` rather than
 * the `<p></p>` ProseMirror actually holds, so "required" checks work unchanged.
 *
 * The formatting on offer is deliberately short: what a teacher needs to write a
 * brief — emphasis, headings, lists, a quote, a link, inline code. No images, no
 * tables, no colour. Everything here survives the server's sanitizer allowlist, so
 * nothing a teacher can type is silently dropped on save.
 */
export function RichTextEditor({
  id,
  value,
  onChange,
  onBlur,
  disabled = false,
  invalid = false,
  placeholder,
  limit = RICH_TEXT_LIMIT,
  className,
}: {
  id?: string;
  value: string;
  onChange: (html: string) => void;
  onBlur?: () => void;
  disabled?: boolean;
  invalid?: boolean;
  placeholder?: string;
  limit?: number;
  className?: string;
}) {
  const [linkDraft, setLinkDraft] = useState<string | null>(null);

  const editor = useEditor({
    // Next renders this on the server first; rendering the editor there would
    // produce markup React then disagrees with on hydration.
    immediatelyRender: false,
    editable: !disabled,
    content: toEditorHtml(value),
    extensions: [
      StarterKit.configure({
        heading: { levels: [2, 3] },
        // A rule across a brief adds a divider nobody asked for; the panel already
        // has edges. Fenced blocks likewise: inline code covers what a brief needs.
        horizontalRule: false,
        codeBlock: false,
        link: {
          openOnClick: false,
          defaultProtocol: 'https',
          // Descriptions are authored by teachers but read by a whole class, so
          // outbound links open away from the app and carry no referrer or ranking.
          HTMLAttributes: { rel: 'noopener noreferrer nofollow', target: '_blank' },
        },
      }),
      Placeholder.configure({ placeholder: placeholder ?? '' }),
      CharacterCount.configure({ limit }),
    ],
    editorProps: {
      attributes: {
        // `rich-text` is the shared prose style — the editor and the published
        // description render through the same rules, so what you type is what
        // students see.
        class: 'rich-text min-h-32 px-3 py-2.5 outline-none',
        ...(id ? { id } : {}),
      },
    },
    onUpdate: ({ editor }) => onChange(editor.isEmpty ? '' : editor.getHTML()),
    onBlur: () => onBlur?.(),
  });

  // Follow `value` when it changes from the outside — a form reset, or an edit
  // dialog reopening on a different assignment. Guarded on equality so the
  // editor is never rebuilt from its own output mid-keystroke, which would drop
  // the caret to the end of the document on every character typed.
  useEffect(() => {
    if (!editor) return;
    const incoming = toEditorHtml(value);
    if (incoming === (editor.isEmpty ? '' : editor.getHTML())) return;
    editor.commands.setContent(incoming, { emitUpdate: false });
  }, [editor, value]);

  useEffect(() => {
    editor?.setEditable(!disabled);
  }, [editor, disabled]);

  const state = useEditorState({
    editor,
    selector: ({ editor }) => ({
      bold: !!editor?.isActive('bold'),
      italic: !!editor?.isActive('italic'),
      strike: !!editor?.isActive('strike'),
      code: !!editor?.isActive('code'),
      h2: !!editor?.isActive('heading', { level: 2 }),
      h3: !!editor?.isActive('heading', { level: 3 }),
      bulletList: !!editor?.isActive('bulletList'),
      orderedList: !!editor?.isActive('orderedList'),
      blockquote: !!editor?.isActive('blockquote'),
      link: !!editor?.isActive('link'),
      canUndo: !!editor?.can().undo(),
      canRedo: !!editor?.can().redo(),
      characters: editor?.storage.characterCount.characters() ?? 0,
    }),
  });

  function openLinkEditor() {
    if (!editor) return;
    setLinkDraft(editor.getAttributes('link').href ?? '');
  }

  function applyLink() {
    if (!editor) return;
    const href = linkDraft?.trim() ?? '';
    const chain = editor.chain().focus().extendMarkRange('link');

    // An emptied box is how you remove a link you no longer want.
    if (!href) chain.unsetLink().run();
    else chain.setLink({ href: /^\w+:/.test(href) ? href : `https://${href}` }).run();

    setLinkDraft(null);
  }

  const remaining = limit - (state?.characters ?? 0);

  return (
    <div
      className={cn(
        'overflow-hidden rounded-lg border border-input bg-transparent text-base transition-colors md:text-sm dark:bg-input/30',
        'focus-within:border-ring focus-within:ring-3 focus-within:ring-ring/50',
        disabled && 'cursor-not-allowed bg-input/50 opacity-50 dark:bg-input/80',
        invalid && 'border-destructive ring-3 ring-destructive/20 dark:border-destructive/50 dark:ring-destructive/40',
        className,
      )}
    >
      <div
        role="toolbar"
        aria-label="Formatting"
        aria-controls={id}
        className="flex flex-wrap items-center gap-0.5 border-b bg-muted/40 px-1.5 py-1"
      >
        <Tool label="Bold" icon={Bold} active={state?.bold} disabled={disabled}
          onClick={() => editor?.chain().focus().toggleBold().run()} />
        <Tool label="Italic" icon={Italic} active={state?.italic} disabled={disabled}
          onClick={() => editor?.chain().focus().toggleItalic().run()} />
        <Tool label="Strikethrough" icon={Strikethrough} active={state?.strike} disabled={disabled}
          onClick={() => editor?.chain().focus().toggleStrike().run()} />
        <Tool label="Inline code" icon={Code} active={state?.code} disabled={disabled}
          onClick={() => editor?.chain().focus().toggleCode().run()} />

        <Divider />

        <Tool label="Heading" icon={Heading2} active={state?.h2} disabled={disabled}
          onClick={() => editor?.chain().focus().toggleHeading({ level: 2 }).run()} />
        <Tool label="Subheading" icon={Heading3} active={state?.h3} disabled={disabled}
          onClick={() => editor?.chain().focus().toggleHeading({ level: 3 }).run()} />

        <Divider />

        <Tool label="Bulleted list" icon={List} active={state?.bulletList} disabled={disabled}
          onClick={() => editor?.chain().focus().toggleBulletList().run()} />
        <Tool label="Numbered list" icon={ListOrdered} active={state?.orderedList} disabled={disabled}
          onClick={() => editor?.chain().focus().toggleOrderedList().run()} />
        <Tool label="Quote" icon={Quote} active={state?.blockquote} disabled={disabled}
          onClick={() => editor?.chain().focus().toggleBlockquote().run()} />

        <Divider />

        <Tool label={state?.link ? 'Edit link' : 'Add link'} icon={Link2} active={state?.link}
          disabled={disabled} onClick={openLinkEditor} />
        <Tool label="Remove link" icon={Link2Off} disabled={disabled || !state?.link}
          onClick={() => editor?.chain().focus().extendMarkRange('link').unsetLink().run()} />

        <div className="ml-auto flex items-center gap-0.5">
          <Tool label="Undo" icon={Undo2} disabled={disabled || !state?.canUndo}
            onClick={() => editor?.chain().focus().undo().run()} />
          <Tool label="Redo" icon={Redo2} disabled={disabled || !state?.canRedo}
            onClick={() => editor?.chain().focus().redo().run()} />
        </div>
      </div>

      {/* The link box replaces a browser prompt: it keeps the selection alive in the
          editor behind it, so applying lands on the words that were highlighted. Closed
          by `disabled` rather than by clearing the draft, so a field that turns read-only
          while it is open cannot leave an editable box on a locked assignment. */}
      {!disabled && linkDraft !== null && (
        <div className="flex items-center gap-1.5 border-b bg-muted/25 px-1.5 py-1.5">
          <Input
            autoFocus
            value={linkDraft}
            placeholder="https://example.com"
            className="h-7 text-sm"
            onChange={(event) => setLinkDraft(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                event.preventDefault();
                applyLink();
              }
              if (event.key === 'Escape') setLinkDraft(null);
            }}
          />
          <Button type="button" size="icon-sm" variant="ghost" onClick={applyLink} aria-label="Apply link">
            <Check className="size-4" />
          </Button>
          <Button type="button" size="icon-sm" variant="ghost" onClick={() => setLinkDraft(null)} aria-label="Cancel">
            <X className="size-4" />
          </Button>
        </div>
      )}

      <EditorContent editor={editor} />

      {/* Silent until it starts to matter — a counter on an empty field is noise. */}
      {!disabled && remaining <= limit / 5 && (
        <p
          aria-live="polite"
          className={cn(
            'border-t px-3 py-1 text-right text-xs text-muted-foreground',
            remaining <= 0 && 'text-danger',
          )}
        >
          {remaining <= 0 ? 'Character limit reached' : `${remaining} characters left`}
        </p>
      )}
    </div>
  );
}

function Tool({
  label,
  icon: Icon,
  active,
  disabled,
  onClick,
}: {
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  active?: boolean;
  disabled?: boolean;
  onClick: () => void;
}) {
  return (
    <Button
      type="button"
      size="icon-sm"
      variant="ghost"
      // `aria-pressed` is both the toggle's accessible state and what tints it, so
      // the two can never disagree.
      aria-pressed={!!active}
      aria-label={label}
      title={label}
      disabled={disabled}
      // Toolbars are not tab stops; the editor is. Arrowing between fifteen buttons
      // to reach the body would be a worse keyboard experience than the shortcuts.
      tabIndex={-1}
      onMouseDown={(event) => event.preventDefault()}
      onClick={onClick}
      className="aria-pressed:bg-background aria-pressed:text-foreground aria-pressed:shadow-xs"
    >
      <Icon className="size-4" />
    </Button>
  );
}

function Divider() {
  return <span aria-hidden className="mx-1 h-4 w-px bg-border" />;
}
