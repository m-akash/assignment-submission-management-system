'use client';

import { Bar, BarChart, CartesianGrid, LabelList, XAxis, YAxis } from 'recharts';
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart';
import type { GradeBandStat } from '@/types/api';

const config = {
  count: { label: 'Submissions', color: 'var(--chart-1)' },
} satisfies ChartConfig;

/**
 * How the marks a teacher has given are spread across the percentage scale.
 *
 * Percentages, not raw marks: an assignment out of 20 and one out of 100 are otherwise not in
 * the same histogram. One series and one colour — the bands are the axis, not five categories,
 * so there is nothing for a second hue or a legend to distinguish.
 *
 * Every band is drawn, including empty ones. A histogram that dropped its low bands would look
 * like a class with no weak results rather than one where nobody scored there.
 */
export function GradeDistributionChart({ data }: { data: GradeBandStat[] }) {
  const rows = data.map((band) => ({
    ...band,
    // Zero is left blank rather than labelled "0" — a caption on an absent bar is noise.
    countLabel: band.count > 0 ? String(band.count) : '',
  }));

  return (
    <ChartContainer config={config} className="aspect-auto h-56 w-full">
      <BarChart accessibilityLayer data={rows} margin={{ left: 4, right: 4, top: 16 }}>
        <CartesianGrid vertical={false} strokeDasharray="0" />
        <XAxis dataKey="band" tickLine={false} axisLine={false} tickMargin={10} />
        <YAxis hide allowDecimals={false} />
        <ChartTooltip
          cursor={false}
          content={<ChartTooltipContent labelKey="band" hideIndicator />}
        />
        {/* No mount animation — see the note in class-rate-chart: a column caught part-way
            through growing is a wrong count on screen. */}
        <Bar
          dataKey="count"
          fill="var(--color-count)"
          radius={[4, 4, 0, 0]}
          maxBarSize={40}
          isAnimationActive={false}
        >
          {/* The y-axis is hidden, so the count on the cap is how a value is read at all —
              it is not a decoration on top of an axis that already says it. */}
          <LabelList
            dataKey="countLabel"
            position="top"
            offset={6}
            className="fill-muted-foreground text-xs tabular-nums"
          />
        </Bar>
      </BarChart>
    </ChartContainer>
  );
}
