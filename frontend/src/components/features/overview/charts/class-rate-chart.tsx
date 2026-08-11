'use client';

import { Bar, BarChart, CartesianGrid, LabelList, XAxis, YAxis } from 'recharts';
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart';
import { classLabel } from '@/lib/format';
import type { ClassActivityStat } from '@/types/api';

const config = {
  rate: { label: 'Submission rate', color: 'var(--chart-1)' },
} satisfies ChartConfig;

/**
 * How much of what each class was set has actually arrived, as a percentage of
 * students × published assignments.
 *
 * Horizontal, because class names are long and would otherwise be rotated tick labels. One
 * colour for every bar, not a light-to-dark ramp: the bar's length already says how big the
 * number is, and colouring by value would spend the only free channel restating it.
 *
 * Sorted by rate, lowest first — the class that is behind is the reason to look at this.
 */
export function ClassRateChart({ data }: { data: ClassActivityStat[] }) {
  const rows = data
    .map((entry) => ({
      label: classLabel(entry.classLevel, entry.classSection),
      // Expected is zero for a class with no students on its roster; the server only sends
      // classes with published work, so this is the one remaining way to divide by nothing.
      rate: entry.expected > 0 ? Math.round((entry.received / entry.expected) * 100) : 0,
      rateLabel: entry.expected > 0 ? `${Math.round((entry.received / entry.expected) * 100)}%` : '—',
      received: entry.received,
      expected: entry.expected,
    }))
    .sort((a, b) => a.rate - b.rate);

  // Enough room per bar to breathe, and the container grows with the rows rather than
  // squeezing twelve classes into a fixed height.
  const height = Math.max(160, rows.length * 34 + 24);

  return (
    <ChartContainer config={config} className="aspect-auto w-full" style={{ height }}>
      <BarChart
        accessibilityLayer
        layout="vertical"
        data={rows}
        margin={{ left: 4, right: 44, top: 4, bottom: 4 }}
      >
        <CartesianGrid horizontal={false} strokeDasharray="0" />
        <XAxis type="number" domain={[0, 100]} hide />
        <YAxis
          type="category"
          dataKey="label"
          tickLine={false}
          axisLine={false}
          width={116}
          tickMargin={6}
        />
        <ChartTooltip
          cursor={false}
          content={
            <ChartTooltipContent
              hideIndicator
              formatter={(_value, _name, item) => (
                <span className="text-muted-foreground">
                  <span className="font-mono font-medium text-foreground tabular-nums">
                    {item.payload.received}
                  </span>
                  {' of '}
                  <span className="font-mono font-medium text-foreground tabular-nums">
                    {item.payload.expected}
                  </span>
                  {' expected'}
                </span>
              )}
            />
          }
        />
        {/* No mount animation: a bar caught part-way through it is a wrong value on screen,
            and this panel re-renders on every theme change and background refetch. */}
        <Bar
          dataKey="rate"
          fill="var(--color-rate)"
          radius={[0, 4, 4, 0]}
          maxBarSize={18}
          isAnimationActive={false}
        >
          {/* The value rides the bar so it is never only in a tooltip. Pre-formatted in the
              row above rather than formatted here — one place decides what "no roster" reads
              as, and it is the same place that decided not to divide. */}
          <LabelList
            dataKey="rateLabel"
            position="right"
            offset={8}
            className="fill-muted-foreground text-xs tabular-nums"
          />
        </Bar>
      </BarChart>
    </ChartContainer>
  );
}
