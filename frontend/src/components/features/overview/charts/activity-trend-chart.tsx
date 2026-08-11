'use client';

import { CartesianGrid, Line, LineChart, XAxis, YAxis } from 'recharts';
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart';
import { formatCalendarDay } from '@/lib/format';
import type { DailyActivityPoint } from '@/types/api';

/**
 * Work arriving against work being marked, one point per day. Two lines on one axis —
 * both series are counts of submissions, so they share a scale and can be read against each
 * other; a second axis would invent a relationship the numbers do not have.
 *
 * Used by both the admin and the teacher overview. The question is the same either way
 * ("is the backlog growing?"); only the scope of the rows behind it differs, and that is
 * settled server-side.
 */
export function ActivityTrendChart({
  data,
  receivedLabel,
}: {
  data: DailyActivityPoint[];
  /** "Submitted" school-wide, "Received" for a teacher's own work. */
  receivedLabel: string;
}) {
  const config = {
    submitted: { label: receivedLabel, color: 'var(--chart-1)' },
    graded: { label: 'Graded', color: 'var(--chart-2)' },
  } satisfies ChartConfig;

  return (
    <ChartContainer config={config} className="aspect-auto h-60 w-full">
      <LineChart accessibilityLayer data={data} margin={{ left: 4, right: 12, top: 4 }}>
        {/* Horizontal only, solid, one step off the surface: the grid is there to be read
            values against, not to be looked at. Dashes would read as a threshold. */}
        <CartesianGrid vertical={false} strokeDasharray="0" />
        <XAxis
          dataKey="date"
          tickLine={false}
          axisLine={false}
          tickMargin={10}
          minTickGap={24}
          tickFormatter={formatCalendarDay}
        />
        <YAxis
          tickLine={false}
          axisLine={false}
          width={28}
          allowDecimals={false}
          tickMargin={4}
        />
        <ChartTooltip
          cursor={{ strokeDasharray: '0' }}
          content={
            <ChartTooltipContent
              indicator="line"
              labelFormatter={(_, payload) => {
                const day = payload?.[0]?.payload?.date;
                return typeof day === 'string' ? formatCalendarDay(day) : '';
              }}
            />
          }
        />
        {/* Two series, so a legend is not optional — colour alone must never be the only
            thing telling them apart. */}
        <ChartLegend content={<ChartLegendContent />} />
        <Line
          dataKey="submitted"
          // Straight segments, not a spline. Most days in a fortnight are empty, and a
          // smoothed curve turns a single busy day into a broad hill — it draws activity on
          // days that had none.
          type="linear"
          stroke="var(--color-submitted)"
          strokeWidth={2}
          strokeLinecap="round"
          dot={false}
          // Growing the line in on mount is a flourish that costs correctness: the panel is
          // re-rendered by every theme change and background refetch, and a chart caught
          // mid-animation draws its start state — a flat line along the baseline.
          isAnimationActive={false}
          // A ring in the panel colour keeps the hovered point legible where the two
          // lines cross, and makes the mark big enough to actually aim at.
          activeDot={{ r: 4, strokeWidth: 2, stroke: 'var(--card)' }}
        />
        <Line
          dataKey="graded"
          // Straight segments, not a spline. Most days in a fortnight are empty, and a
          // smoothed curve turns a single busy day into a broad hill — it draws activity on
          // days that had none.
          type="linear"
          stroke="var(--color-graded)"
          strokeWidth={2}
          strokeLinecap="round"
          dot={false}
          // Growing the line in on mount is a flourish that costs correctness: the panel is
          // re-rendered by every theme change and background refetch, and a chart caught
          // mid-animation draws its start state — a flat line along the baseline.
          isAnimationActive={false}
          activeDot={{ r: 4, strokeWidth: 2, stroke: 'var(--card)' }}
        />
      </LineChart>
    </ChartContainer>
  );
}
