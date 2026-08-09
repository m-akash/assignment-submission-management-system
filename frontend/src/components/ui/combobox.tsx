'use client';

import * as React from 'react';
import { Popover as PopoverPrimitive } from 'radix-ui';
import { Check, ChevronDown, Search, X } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

export interface ComboboxOption {
  value: string;
  label: string;
  /** Secondary text shown under the label and matched by the search box too. */
  hint?: string;
  disabled?: boolean;
}

/**
 * A picker with a search box, in single- and multi-select flavours.
 *
 * Radix's Select can't hold a search field — it owns the keyboard and type-ahead selects
 * rather than filters — so this is built on Popover with an explicit listbox instead. The
 * trigger keeps Select's shape and focus ring so the two read as the same control, which
 * matters while both exist in the codebase.
 *
 * Options are filtered in the browser: every list that feeds one of these fetches its
 * whole option set already (classes, courses, offerings — reference data in the hundreds
 * at most), so a round trip per keystroke would buy nothing.
 */
const TRIGGER_CLASS =
  "flex h-8 w-full items-center justify-between gap-1.5 rounded-lg border border-input bg-transparent py-2 pr-2 pl-2.5 text-sm whitespace-nowrap transition-colors outline-none select-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20 dark:bg-input/30 dark:hover:bg-input/50 dark:aria-invalid:border-destructive/50 dark:aria-invalid:ring-destructive/40 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4";

interface SharedProps {
  options: ComboboxOption[];
  /** Shown on the trigger while nothing is selected. */
  placeholder?: string;
  searchPlaceholder?: string;
  emptyMessage?: string;
  disabled?: boolean;
  /** Sizes the trigger — the popover matches its width. */
  className?: string;
  id?: string;
  'aria-label'?: string;
  'aria-invalid'?: boolean;
}

export function Combobox({
  value,
  onChange,
  clearable = false,
  ...shared
}: SharedProps & {
  value: string;
  onChange: (value: string) => void;
  /** Adds an X on the trigger that resets the value to "". */
  clearable?: boolean;
}) {
  return (
    <ComboboxBase
      {...shared}
      multiple={false}
      selected={value ? [value] : []}
      clearable={clearable}
      onToggle={(next) => onChange(next)}
      onClear={() => onChange('')}
    />
  );
}

export function MultiCombobox({
  values,
  onChange,
  clearable = true,
  ...shared
}: SharedProps & {
  values: string[];
  onChange: (values: string[]) => void;
  clearable?: boolean;
}) {
  return (
    <ComboboxBase
      {...shared}
      multiple
      selected={values}
      clearable={clearable}
      onToggle={(next) =>
        onChange(
          values.includes(next) ? values.filter((value) => value !== next) : [...values, next],
        )
      }
      onClear={() => onChange([])}
    />
  );
}

function ComboboxBase({
  options,
  selected,
  multiple,
  clearable,
  onToggle,
  onClear,
  placeholder = 'Select…',
  searchPlaceholder = 'Search…',
  emptyMessage = 'No matches',
  disabled,
  className,
  id,
  'aria-label': ariaLabel,
  'aria-invalid': ariaInvalid,
}: SharedProps & {
  selected: string[];
  multiple: boolean;
  clearable: boolean;
  onToggle: (value: string) => void;
  onClear: () => void;
}) {
  const [open, setOpen] = React.useState(false);
  const [query, setQuery] = React.useState('');
  const [activeIndex, setActiveIndex] = React.useState(0);
  const listRef = React.useRef<HTMLDivElement>(null);
  const triggerRef = React.useRef<HTMLButtonElement>(null);
  const listboxId = React.useId();
  const optionId = (index: number) => `${listboxId}-option-${index}`;

  const matches = React.useMemo(() => {
    const term = query.trim().toLowerCase();
    if (!term) return options;
    return options.filter(
      (option) =>
        option.label.toLowerCase().includes(term) || option.hint?.toLowerCase().includes(term),
    );
  }, [options, query]);

  // Keeps the highlight on a real row after the list shrinks under the search box.
  const active = Math.min(activeIndex, Math.max(matches.length - 1, 0));

  function openAt(nextOpen: boolean) {
    setOpen(nextOpen);
    if (nextOpen) {
      setQuery('');
      // Single-select opens on what is already chosen; multi has no single "current" row.
      const current = multiple ? -1 : options.findIndex((option) => option.value === selected[0]);
      setActiveIndex(current < 0 ? 0 : current);
    }
  }

  // Follows the highlight when it moves past the edge of the scroll box. Runs on `active`
  // rather than inside the key handler so a shrinking list scrolls back up too.
  React.useEffect(() => {
    if (!open) return;
    listRef.current?.querySelector('[data-active="true"]')?.scrollIntoView({ block: 'nearest' });
  }, [active, open, matches.length]);

  function step(direction: 1 | -1) {
    if (matches.length === 0) return;
    let next = active;
    // Walks past disabled rows, and stops rather than wrapping into an infinite loop when
    // every row is disabled.
    for (let hops = 0; hops < matches.length; hops += 1) {
      next = (next + direction + matches.length) % matches.length;
      if (!matches[next].disabled) break;
    }
    setActiveIndex(next);
  }

  function onKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        step(1);
        break;
      case 'ArrowUp':
        event.preventDefault();
        step(-1);
        break;
      case 'Home':
        event.preventDefault();
        setActiveIndex(0);
        break;
      case 'End':
        event.preventDefault();
        setActiveIndex(matches.length - 1);
        break;
      case 'Enter': {
        // Always swallowed: these live inside forms, where a stray Enter would submit.
        event.preventDefault();
        const option = matches[active];
        if (option && !option.disabled) choose(option.value);
        break;
      }
      default:
        break;
    }
  }

  function choose(value: string) {
    onToggle(value);
    // Multi-select stays open — picking several is the whole point — and keeps the search
    // term so a narrowed list can be worked through.
    if (!multiple) {
      setOpen(false);
      triggerRef.current?.focus();
    }
  }

  const chosen = options.filter((option) => selected.includes(option.value));
  const showClear = clearable && selected.length > 0 && !disabled;

  return (
    <div className={cn('relative', className)}>
      <PopoverPrimitive.Root open={open} onOpenChange={openAt}>
        <PopoverPrimitive.Trigger asChild>
          <button
            ref={triggerRef}
            type="button"
            id={id}
            role="combobox"
            aria-expanded={open}
            aria-haspopup="listbox"
            // Points at the listbox the popover renders while open. Named unconditionally so
            // the role's required attributes are always present.
            aria-controls={listboxId}
            aria-label={ariaLabel}
            aria-invalid={ariaInvalid}
            disabled={disabled}
            data-slot="combobox-trigger"
            className={cn(TRIGGER_CLASS, showClear && '[&>svg]:invisible')}
          >
            {selected.length === 0 ? (
              <span className="truncate text-muted-foreground">{placeholder}</span>
            ) : (
              <span className="flex min-w-0 items-center gap-1.5">
                {/* An id with no matching option means the option list has not loaded yet
                    (or the value came from a URL); showing it raw beats showing nothing. */}
                <span className="truncate">{chosen[0]?.label ?? selected[0]}</span>
                {selected.length > 1 && (
                  <Badge variant="secondary" className="shrink-0 px-1.5 font-normal tabular-nums">
                    +{selected.length - 1}
                  </Badge>
                )}
              </span>
            )}
            <ChevronDown className="pointer-events-none size-4 text-muted-foreground" />
          </button>
        </PopoverPrimitive.Trigger>

        <PopoverPrimitive.Portal>
          <PopoverPrimitive.Content
            data-slot="combobox-content"
            align="start"
            sideOffset={4}
            className="z-50 w-(--radix-popover-trigger-width) min-w-48 origin-(--radix-popover-content-transform-origin) rounded-lg bg-popover text-popover-foreground shadow-md ring-1 ring-foreground/10 duration-100 data-[side=bottom]:slide-in-from-top-2 data-[side=top]:slide-in-from-bottom-2 data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95"
          >
            <div className="flex items-center gap-2 border-b px-2.5">
              <Search className="size-4 shrink-0 text-muted-foreground" />
              <input
                value={query}
                onChange={(event) => {
                  setQuery(event.target.value);
                  setActiveIndex(0);
                }}
                onKeyDown={onKeyDown}
                placeholder={searchPlaceholder}
                aria-label={searchPlaceholder}
                aria-controls={listboxId}
                aria-autocomplete="list"
                // The highlight moves with the arrow keys while focus stays in this box, so
                // the active row has to be named here for a screen reader to follow it.
                aria-activedescendant={matches.length > 0 ? optionId(active) : undefined}
                className="h-9 w-full bg-transparent text-sm outline-none placeholder:text-muted-foreground"
              />
            </div>

            <div
              ref={listRef}
              id={listboxId}
              role="listbox"
              aria-multiselectable={multiple || undefined}
              className="max-h-64 overflow-y-auto overflow-x-hidden p-1"
            >
              {matches.length === 0 ? (
                <p className="px-1.5 py-4 text-center text-sm text-muted-foreground">
                  {emptyMessage}
                </p>
              ) : (
                matches.map((option, index) => {
                  const isSelected = selected.includes(option.value);
                  return (
                    <div
                      key={option.value}
                      id={optionId(index)}
                      role="option"
                      aria-selected={isSelected}
                      aria-disabled={option.disabled || undefined}
                      data-active={index === active}
                      // Pointer, not click: the search input keeps focus, so there is no
                      // blur to race with, and the row responds on press like a menu item.
                      onPointerDown={(event) => {
                        event.preventDefault();
                        if (!option.disabled) choose(option.value);
                      }}
                      onPointerMove={() => !option.disabled && setActiveIndex(index)}
                      className={cn(
                        'flex cursor-default items-center gap-2 rounded-md py-1.5 pr-2 pl-1.5 text-sm outline-hidden select-none',
                        'data-[active=true]:bg-accent data-[active=true]:text-accent-foreground',
                        option.disabled && 'pointer-events-none opacity-50',
                      )}
                    >
                      <Check
                        className={cn(
                          'size-4 shrink-0 text-primary',
                          !isSelected && 'invisible',
                        )}
                      />
                      <span className="min-w-0 flex-1">
                        <span className="block truncate">{option.label}</span>
                        {option.hint && (
                          <span className="block truncate text-xs text-muted-foreground">
                            {option.hint}
                          </span>
                        )}
                      </span>
                    </div>
                  );
                })
              )}
            </div>

            {multiple && selected.length > 0 && (
              <div className="flex items-center justify-between gap-2 border-t px-2.5 py-1.5 text-xs text-muted-foreground">
                <span>{selected.length} selected</span>
                <button
                  type="button"
                  onClick={onClear}
                  className="rounded-sm font-medium transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:outline-none"
                >
                  Clear
                </button>
              </div>
            )}
          </PopoverPrimitive.Content>
        </PopoverPrimitive.Portal>
      </PopoverPrimitive.Root>

      {/* Sits over the chevron rather than inside the trigger — a button cannot nest one. */}
      {showClear && (
        <button
          type="button"
          aria-label="Clear selection"
          title="Clear selection"
          onClick={() => {
            onClear();
            triggerRef.current?.focus();
          }}
          className="absolute top-1/2 right-2 flex size-4 -translate-y-1/2 items-center justify-center rounded-sm text-muted-foreground transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:outline-none"
        >
          <X className="size-3.5" />
        </button>
      )}
    </div>
  );
}
