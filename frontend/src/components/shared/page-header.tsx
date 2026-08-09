import Link from 'next/link';
import { ArrowLeft } from 'lucide-react';

/**
 * The way back to wherever a page was reached from. It sits above the title on every
 * screen that has a parent, so "where am I and how do I leave" is answered in one place
 * rather than by a decorative icon that said nothing the title did not already say.
 */
export function BackLink({ href, label }: { href: string; label: string }) {
  return (
    <Link
      href={href}
      className="group inline-flex max-w-full items-center gap-2.5 text-sm font-medium text-muted-foreground transition-colors hover:text-foreground"
    >
      <span className="flex size-8 shrink-0 items-center justify-center rounded-lg border bg-card shadow-xs transition-colors group-hover:border-primary/40 group-hover:bg-accent group-hover:text-primary">
        <ArrowLeft className="size-4 transition-transform duration-200 group-hover:-translate-x-0.5" />
      </span>
      <span className="truncate">{label}</span>
    </Link>
  );
}

/**
 * The top of every screen: what this page is, in one line, with its primary action on
 * the right. The optional eyebrow names the group the page belongs to, so a title can
 * stay short ("Course Offerings") without losing context ("Administration").
 */
export function PageHeader({
  back,
  eyebrow,
  title,
  description,
  actions,
}: {
  back?: { href: string; label: string };
  eyebrow?: string;
  title: string;
  description?: string;
  actions?: React.ReactNode;
}) {
  return (
    <div className="space-y-4">
      {back && <BackLink href={back.href} label={back.label} />}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0 space-y-1.5">
          {eyebrow && <p className="eyebrow">{eyebrow}</p>}
          <h1 className="text-2xl leading-tight font-semibold text-balance lg:text-[1.75rem]">
            {title}
          </h1>
          {description && (
            <p className="max-w-2xl text-sm text-muted-foreground">{description}</p>
          )}
        </div>
        {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
      </div>
    </div>
  );
}
