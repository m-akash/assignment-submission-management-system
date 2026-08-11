'use client';

import { CartesianGrid, Line, LineChart, ReferenceLine, XAxis, YAxis } from 'recharts';
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart';
import type { MarkPointStat } from '@/types/api';

const config = {
  percent: { label: 'Mark', color: 'var(--chart-1)' },
} satisfies ChartConfig;

/**
 * A student's marks in the order they were given, as percentages.
 *
 * Percentages rather than raw marks, so an assignment out of 20 sits on the same line as one
 * out of 100. The x-axis is the course code of each piece of work rather than a date: the
 * marks arrive irregularly, and what a student wants to see is "which subjects am I dropping
 * in", not the calendar spacing between them.
 *
 * One series, so no legend — the panel title says what is plotted. The average is drawn as a
 * reference line instead, which is the comparison a single line actually needs.
 */
export function MarksTrendChart({ data }: { data: MarkPointStat[] }) {
  const average =
    data.length > 0
      ? Math.round(data.reduce((sum, point) => sum + point.percent, 0) / data.length)
      : 0;

  // Only worth drawing when the marks actually vary. With every mark the same the average
  // lies exactly on the data line, so the rule adds no comparison and its label lands on top
  // of the line it duplicates.
  const varies =
    data.length > 2 && new Set(data.map((point) => point.percent)).size > 1;

  return (
    <ChartContainer config={config} className="aspect-auto h-60 w-full">
      <LineChart accessibilityLayer data={data} margin={{ left: 4, right: 12, top: 8 }}>
        <CartesianGrid vertical={false} strokeDasharray="0" />
        <XAxis
          dataKey="courseCode"
          tickLine={false}
          axisLine={false}
          tickMargin={10}
          minTickGap={8}
        />
        <YAxis
          domain={[0, 100]}
          ticks={[0, 25, 50, 75, 100]}
          tickLine={false}
          axisLine={false}
          width={34}
          tickMargin={4}
          tickFormatter={(value: number) => `${value}%`}
        />
        <ChartTooltip
          cursor={{ strokeDasharray: '0' }}
          content={
            <ChartTooltipContent
              indicator="line"
              labelKey="assignmentTitle"
              formatter={(value) => (
                <span className="font-mono font-medium text-foreground tabular-nums">
                  {value}%
                </span>
              )}
            />
          }
        />
        {/* Solid hairline, like the grid: dashing it would read as a target or a projection
            rather than "this is where you actually are". */}
        {varies && (
          <ReferenceLine
            y={average}
            stroke="var(--muted-foreground)"
            strokeWidth={1}
            label={{
              value: `Average ${average}%`,
              position: 'insideTopRight',
              className: 'fill-muted-foreground text-xs',
            }}
          />
        )}
        <Line
          dataKey="percent"
          type="monotone"
          stroke="var(--color-percent)"
          strokeWidth={2}
          strokeLinecap="round"
          // Every mark is a real event worth pointing at, and there are at most twenty of
          // them — so the dots stay, each ringed in the panel colour to survive overlap.
          dot={{ r: 3, strokeWidth: 2, stroke: 'var(--card)', fill: 'var(--color-percent)' }}
          activeDot={{ r: 5, strokeWidth: 2, stroke: 'var(--card)' }}
          // See the note in activity-trend-chart: mid-animation this draws a flat line along
          // the baseline, which is a claim about the marks rather than a transition.
          isAnimationActive={false}
        />
      </LineChart>
    </ChartContainer>
  );
}
