import type { LucideIcon } from 'lucide-react';

/**
 * The top of every screen: what this page is, in one line, with its primary action on
 * the right. The optional eyebrow names the group the page belongs to, so a title can
 * stay short ("Course Offerings") without losing context ("Administration").
 */
export function PageHeader({
  eyebrow,
  title,
  description,
  icon: Icon,
  actions,
}: {
  eyebrow?: string;
  title: string;
  description?: string;
  icon?: LucideIcon;
  actions?: React.ReactNode;
}) {
  return (
    <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
      <div className="flex min-w-0 items-start gap-3.5">
        {Icon && (
          <span className="mt-0.5 hidden size-11 shrink-0 items-center justify-center rounded-xl border bg-card text-primary shadow-xs sm:flex">
            <Icon className="size-5" />
          </span>
        )}
        <div className="min-w-0 space-y-1.5">
          {eyebrow && <p className="eyebrow">{eyebrow}</p>}
          <h1 className="text-2xl leading-tight font-semibold text-balance lg:text-[1.75rem]">
            {title}
          </h1>
          {description && (
            <p className="max-w-2xl text-sm text-muted-foreground">{description}</p>
          )}
        </div>
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </div>
  );
}
