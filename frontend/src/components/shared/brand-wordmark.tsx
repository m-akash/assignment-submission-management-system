import { GraduationCap } from 'lucide-react';
import { cn } from '@/lib/utils';

/**
 * The product wordmark, as a two-line lockup.
 *
 * Set as one long string, "Assignment Management System" reads as a sentence and has
 * to shrink to fit its row. Split, the first word can carry the display face at a size
 * worth looking at and the rest becomes a quiet strapline beneath it — the same
 * structure the sidebar header uses, so the auth screens and the app agree.
 *
 * `tone="onBrand"` is for the indigo auth panel, where the mark has to be white and
 * the tile is a translucent wash of the surface it sits on. The default tone is for
 * light and dark card backgrounds, where the tile carries the brand gradient instead.
 */
export function BrandWordmark({
  tone = 'default',
  size = 'sm',
  className,
  ...props
}: React.ComponentProps<'div'> & {
  tone?: 'default' | 'onBrand';
  size?: 'sm' | 'lg';
}) {
  const onBrand = tone === 'onBrand';
  const large = size === 'lg';

  return (
    <div className={cn('flex items-center', large ? 'gap-3.5' : 'gap-3', className)} {...props}>
      <span
        className={cn(
          'flex shrink-0 items-center justify-center text-white',
          large ? 'size-13 rounded-2xl' : 'size-11 rounded-xl',
          onBrand ? 'bg-white/15 ring-1 ring-white/25 backdrop-blur' : 'brand-surface shadow-sm',
        )}
      >
        <GraduationCap className={large ? 'size-7' : 'size-6'} />
      </span>

      <span className="grid min-w-0">
        <span
          className={cn(
            'truncate font-heading leading-none font-bold tracking-tight',
            large ? 'text-[1.85rem]' : 'text-xl',
            onBrand && 'text-white',
          )}
        >
          Assignment
        </span>
        <span
          className={cn(
            'truncate tracking-[0.08em]',
            large ? 'mt-1.5 text-[0.85rem]' : 'mt-1 text-[0.75rem]',
            onBrand ? 'text-white/70' : 'text-muted-foreground',
          )}
        >
          Management System
        </span>
      </span>
    </div>
  );
}
