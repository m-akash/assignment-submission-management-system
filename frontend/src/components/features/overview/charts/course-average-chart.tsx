'use client';

import { Bar, BarChart, CartesianGrid, LabelList, XAxis, YAxis } from 'recharts';
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart';
import type { CourseAverageStat } from '@/types/api';

const config = {
  averagePercent: { label: 'Average', color: 'var(--chart-1)' },
} satisfies ChartConfig;

/**
 * A student's average per course, as a percentage of what each piece of work was out of.
 *
 * Horizontal so course names read straight, and one colour for every bar — the length is the
 * value, and a light-to-dark ramp would say it twice while using up the channel that could
 * have carried something the chart does not already show.
 *
 * Sorted lowest first: the subject a student is behind in is why they opened this.
 */
export function CourseAverageChart({ data }: { data: CourseAverageStat[] }) {
  const rows = [...data]
    .sort((a, b) => a.averagePercent - b.averagePercent)
    .map((course) => ({
      ...course,
      label: `${Math.round(course.averagePercent)}%`,
    }));

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
          dataKey="courseName"
          tickLine={false}
          axisLine={false}
          width={116}
          tickMargin={6}
          tickFormatter={(name: string) => (name.length > 16 ? `${name.slice(0, 15)}…` : name)}
        />
        <ChartTooltip
          cursor={false}
          content={
            <ChartTooltipContent
              labelKey="courseName"
              hideIndicator
              formatter={(value, _name, item) => (
                <span className="text-muted-foreground">
                  <span className="font-mono font-medium text-foreground tabular-nums">
                    {value}%
                  </span>
                  {` across ${item.payload.gradedCount} marked`}
                </span>
              )}
            />
          }
        />
        <Bar
          dataKey="averagePercent"
          fill="var(--color-averagePercent)"
          radius={[0, 4, 4, 0]}
          maxBarSize={18}
          // See the note in class-rate-chart: a bar caught mid-mount is a wrong average.
          isAnimationActive={false}
        >
          <LabelList
            dataKey="label"
            position="right"
            offset={8}
            className="fill-muted-foreground text-xs tabular-nums"
          />
        </Bar>
      </BarChart>
    </ChartContainer>
  );
}
