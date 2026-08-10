'use client';

import type { FormEventHandler, ReactNode } from 'react';
import { Loader2, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Drawer,
  DrawerClose,
  DrawerContent,
  DrawerDescription,
  DrawerFooter,
  DrawerHeader,
  DrawerTitle,
} from '@/components/ui/drawer';
import { cn } from '@/lib/utils';

/**
 * DrawerContent carries its own width, qualified by the direction it opens from
 * (`data-[vaul-drawer-direction=right]:sm:max-w-sm`). A bare `sm:max-w-md` alongside it
 * loses on specificity and is not close enough for tailwind-merge to drop, so the override
 * has to repeat the qualifier — which is what this table is for.
 */
const WIDTHS = {
  sm: 'data-[vaul-drawer-direction=right]:sm:max-w-sm',
  md: 'data-[vaul-drawer-direction=right]:sm:max-w-md',
  lg: 'data-[vaul-drawer-direction=right]:sm:max-w-lg',
} as const;

/**
 * The shape every create and edit form takes: a panel off the right edge with the title
 * pinned to the top, the actions pinned to the bottom, and only the fields between them
 * scrolling.
 *
 * A panel rather than a centred modal because these forms grow — a user gains a class and
 * an academic year the moment the role is Student — and a modal that outgrows the viewport
 * either scrolls its own buttons out of reach or has to be capped at a height it then
 * fights. The panel is already full height, so a longer form only means a longer scroll
 * area and the submit button stays where it was.
 *
 * Confirmations stay modal: they are one sentence and one decision, and a slide-in panel
 * for "delete this?" is a lot of motion for a yes/no.
 */
export function FormDrawer({
  open,
  onOpenChange,
  title,
  description,
  submitLabel,
  submitting,
  onSubmit,
  width = 'md',
  className,
  children,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: ReactNode;
  description?: ReactNode;
  /** Label on the primary button — "Create course", "Save changes", "Assign". */
  submitLabel: ReactNode;
  /** Disables the primary button and shows a spinner while the mutation is in flight. */
  submitting?: boolean;
  onSubmit: FormEventHandler<HTMLFormElement>;
  /** How wide the panel opens. A two-field form reads better narrow than padded out. */
  width?: keyof typeof WIDTHS;
  className?: string;
  children: ReactNode;
}) {
  return (
    <Drawer open={open} onOpenChange={onOpenChange} direction="right">
      <DrawerContent
        // Not every form needs a second sentence. Clearing the attribute for those tells
        // Radix the omission is deliberate rather than a description it failed to find.
        {...(description ? {} : { 'aria-describedby': undefined })}
        className={cn(WIDTHS[width], className)}
      >
        <DrawerHeader className="border-b pr-12">
          <DrawerTitle>{title}</DrawerTitle>
          {description ? <DrawerDescription>{description}</DrawerDescription> : null}
        </DrawerHeader>

        <DrawerClose asChild>
          <Button variant="ghost" size="icon-sm" className="absolute top-3 right-3">
            <X />
            <span className="sr-only">Close</span>
          </Button>
        </DrawerClose>

        <form onSubmit={onSubmit} className="flex min-h-0 flex-1 flex-col" noValidate>
          <div className="min-h-0 flex-1 space-y-4 overflow-y-auto p-4">{children}</div>

          <DrawerFooter className="flex-row justify-end border-t bg-muted/50">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={submitting}>
              {submitting && <Loader2 className="size-4 animate-spin" />}
              {submitLabel}
            </Button>
          </DrawerFooter>
        </form>
      </DrawerContent>
    </Drawer>
  );
}
