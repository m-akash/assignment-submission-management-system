import { cn } from '@/lib/utils';

/**
 * The brand band at the top of each dashboard. One component, three roles: it is the
 * only saturated surface in the app, which is what makes the rest of the page read as
 * calm. Anything that isn't a greeting, a sentence of orientation, or a primary action
 * belongs in a panel below it, not here.
 */
export function HeroBanner({
  eyebrow,
  title,
  description,
  actions,
  aside,
}: {
  eyebrow?: string;
  title: string;
  description?: string;
  actions?: React.ReactNode;
  /** Optional right-hand slot — a metric, ring, or short list. */
  aside?: React.ReactNode;
}) {
  return (
    <section className="brand-surface relative overflow-hidden rounded-2xl p-6 text-white shadow-lg sm:p-8">
      <div aria-hidden className="brand-dots pointer-events-none absolute inset-0 opacity-35" />
      <div
        aria-hidden
        className="pointer-events-none absolute -top-28 -right-20 size-80 rounded-full bg-white/12 blur-3xl"
      />

      <div className="relative flex flex-col gap-7 lg:flex-row lg:items-center lg:justify-between">
        <div className="max-w-xl space-y-2">
          {eyebrow && (
            <p className="text-[0.7rem] font-medium tracking-[0.18em] text-white/70 uppercase">
              {eyebrow}
            </p>
          )}
          <h1 className="text-2xl leading-tight font-semibold text-balance sm:text-[1.8rem]">
            {title}
          </h1>
          {description && <p className="text-sm/relaxed text-white/80">{description}</p>}
          {actions && <div className="flex flex-wrap items-center gap-2 pt-3">{actions}</div>}
        </div>
        {aside && <div className="shrink-0">{aside}</div>}
      </div>
    </section>
  );
}

/** Buttons sitting on the brand surface need their own two levels of emphasis. */
export const heroButton = {
  solid: 'bg-white text-[var(--brand)] shadow-sm hover:bg-white/90',
  quiet: 'border-white/25 bg-white/10 text-white hover:bg-white/20 dark:bg-white/10',
} as const;

/** A single figure in the hero's right-hand slot. */
export function HeroStat({
  value,
  label,
  className,
}: {
  value: React.ReactNode;
  label: string;
  className?: string;
}) {
  return (
    <div
      className={cn(
        'rounded-xl bg-white/10 px-4 py-3 text-center ring-1 ring-white/20 ring-inset backdrop-blur-sm',
        className,
      )}
    >
      <p className="font-heading text-xl leading-none font-semibold tabular-nums">{value}</p>
      <p className="mt-1.5 text-[0.7rem] tracking-wide text-white/70 uppercase">{label}</p>
    </div>
  );
}
