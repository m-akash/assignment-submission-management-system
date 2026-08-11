'use client';

import { Bar, BarChart, XAxis, YAxis } from 'recharts';
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart';
import { SHARE_STEPS } from './share-bar';
import type { AssignmentProgressStat } from '@/types/api';

/**
 * One bar per published assignment, split across the class it was set for: marked, waiting to
 * be marked, and never handed in. The three add up to the roster, so the length of a bar is
 * the class and the segments are where that class has got to.
 *
 * The segments are steps of one hue rather than three separate colours. Done → waiting →
 * missing is an ordered scale, so the progression belongs in the colour; three hues would say
 * "three unrelated things" and would also have to survive a colour-blindness check that
 * lightness steps pass by construction.
 */
export function AssignmentProgressChart({ data }: { data: AssignmentProgressStat[] }) {
  const config = {
    graded: { label: 'Marked', color: SHARE_STEPS[0] },
    awaitingMarking: { label: 'To mark', color: SHARE_STEPS[1] },
    notSubmitted: { label: 'Not handed in', color: SHARE_STEPS[2] },
  } satisfies ChartConfig;

  // The biggest class on the chart. Fixing the axis to it is what makes a bar's length mean
  // "a class": left to pick its own round number, recharts pads the axis past the largest
  // total, so every bar stopped short of the panel and a full roster read as a partial one.
  const largestClass = data.reduce(
    (max, row) => Math.max(max, row.graded + row.awaitingMarking + row.notSubmitted),
    0,
  );

  const height = Math.max(180, data.length * 38 + 40);

  return (
    <ChartContainer config={config} className="aspect-auto w-full" style={{ height }}>
      <BarChart
        accessibilityLayer
        layout="vertical"
        data={data}
        margin={{ left: 4, right: 12, top: 4 }}
      >
        <XAxis type="number" domain={[0, largestClass || 1]} allowDecimals={false} hide />
        <YAxis
          type="category"
          dataKey="title"
          tickLine={false}
          axisLine={false}
          width={190}
          tickMargin={6}
          // A brief's title is a sentence; cut it rather than let it push the plot off the
          // panel. The full title is in the tooltip and on the assignments page.
          tickFormatter={(title: string) => (title.length > 28 ? `${title.slice(0, 27)}…` : title)}
        />
        <ChartTooltip
          cursor={false}
          content={<ChartTooltipContent labelKey="title" indicator="dot" />}
        />
        <ChartLegend content={<ChartLegendContent />} />
        {/* stroke in the panel's own colour is the 2px gap between segments — SVG has no
            other way to space a stack, and a contrasting outline would add ink that is not
            data. Only the outer ends are rounded, so the bar reads as one length.
            Animation is off throughout: a stack caught mid-mount shows segments that do not
            add up to the roster, and this panel re-renders on theme changes and refetches. */}
        <Bar
          dataKey="graded"
          stackId="roster"
          fill="var(--color-graded)"
          stroke="var(--card)"
          strokeWidth={2}
          radius={[0, 0, 0, 0]}
          maxBarSize={20}
          isAnimationActive={false}
        />
        <Bar
          dataKey="awaitingMarking"
          stackId="roster"
          fill="var(--color-awaitingMarking)"
          stroke="var(--card)"
          strokeWidth={2}
          maxBarSize={20}
          isAnimationActive={false}
        />
        <Bar
          dataKey="notSubmitted"
          stackId="roster"
          fill="var(--color-notSubmitted)"
          stroke="var(--card)"
          strokeWidth={2}
          radius={[0, 4, 4, 0]}
          maxBarSize={20}
          isAnimationActive={false}
        />
      </BarChart>
    </ChartContainer>
  );
}
