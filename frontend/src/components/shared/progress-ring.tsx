import { cn } from '@/lib/utils';

/**
 * A single percentage, drawn. Used for a student's average mark, where "72%" alone
 * carries no sense of scale — the arc gives it one at a glance.
 */
export function ProgressRing({
  value,
  caption,
  size = 116,
  thickness = 9,
  className,
  trackClassName = 'stroke-white/20',
  barClassName = 'stroke-white',
}: {
  /** 0–100, or null when there is nothing to show yet. */
  value: number | null;
  caption?: string;
  size?: number;
  thickness?: number;
  className?: string;
  trackClassName?: string;
  barClassName?: string;
}) {
  const radius = (size - thickness) / 2;
  const circumference = 2 * Math.PI * radius;
  const clamped = value === null ? 0 : Math.min(100, Math.max(0, value));

  return (
    <div
      className={cn('relative', className)}
      style={{ width: size, height: size }}
      role="img"
      aria-label={value === null ? `${caption ?? 'Progress'}: no data yet` : `${caption ?? 'Progress'}: ${clamped}%`}
    >
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} className="-rotate-90">
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          strokeWidth={thickness}
          className={trackClassName}
        />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          strokeWidth={thickness}
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={circumference * (1 - clamped / 100)}
          className={cn('transition-[stroke-dashoffset] duration-1000 ease-out', barClassName)}
        />
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="font-heading text-xl leading-none font-semibold tabular-nums">
          {value === null ? '—' : `${clamped}%`}
        </span>
        {caption && (
          <span className="mt-1 text-[0.65rem] tracking-wide uppercase opacity-70">{caption}</span>
        )}
      </div>
    </div>
  );
}
