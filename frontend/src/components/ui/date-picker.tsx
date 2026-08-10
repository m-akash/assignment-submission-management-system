'use client';

import * as React from 'react';
import { CalendarIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Calendar } from '@/components/ui/calendar';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';

/**
 * The shadcn date picker: a `Popover` holding a `Calendar`, rather than the browser's
 * native `type="date"` — which renders differently in every browser and cannot be themed.
 *
 * Both pickers speak the same strings the forms and the Zod schemas already use, so the
 * calendar is the only thing that changed: `DatePicker` reads and writes "YYYY-MM-DD"
 * (what the API returns for an academic year), and `DateTimePicker` reads and writes
 * "YYYY-MM-DDTHH:mm" in the reader's own zone (what `datetime-local` used to produce).
 *
 * Those strings are built by hand from the local date parts rather than through
 * `toISOString()`, which would shift the day backwards for anyone west of UTC.
 */

/** "YYYY-MM-DD" for a Date, in the reader's zone. */
function toDateValue(date: Date): string {
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

/** "HH:mm" for a Date, in the reader's zone. */
function toTimeValue(date: Date): string {
  return `${`${date.getHours()}`.padStart(2, '0')}:${`${date.getMinutes()}`.padStart(2, '0')}`;
}

/**
 * The Date a "YYYY-MM-DD[THH:mm]" value names, or undefined while the field is empty or
 * half-typed. Parsed part by part so the value is always read as local time — `new Date`
 * treats a bare "YYYY-MM-DD" as UTC.
 */
function parseValue(value: string | undefined): Date | undefined {
  if (!value) return undefined;

  const match = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2}))?$/.exec(value);
  if (!match) return undefined;

  const [, year, month, day, hours, minutes] = match;
  const date = new Date(
    Number(year),
    Number(month) - 1,
    Number(day),
    Number(hours ?? 0),
    Number(minutes ?? 0),
  );

  return Number.isNaN(date.getTime()) ? undefined : date;
}

function formatDay(date: Date): string {
  return date.toLocaleDateString(undefined, {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  });
}

export function DatePicker({
  id,
  value,
  onChange,
  onBlur,
  placeholder = 'Pick a date',
  disabled,
  invalid,
  className,
}: {
  id?: string;
  /** "YYYY-MM-DD", or "" for no date. */
  value: string;
  onChange: (value: string) => void;
  onBlur?: () => void;
  placeholder?: string;
  disabled?: boolean;
  invalid?: boolean;
  className?: string;
}) {
  const [open, setOpen] = React.useState(false);
  const selected = parseValue(value);

  return (
    <Popover
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        // Closing the calendar is the moment the field is done being edited, which is
        // what react-hook-form's touched state is waiting for.
        if (!next) onBlur?.();
      }}
    >
      <PopoverTrigger asChild>
        <Button
          id={id}
          type="button"
          variant="outline"
          disabled={disabled}
          aria-invalid={invalid}
          className={cn(
            'w-full justify-start font-normal',
            !selected && 'text-muted-foreground',
            className,
          )}
        >
          <CalendarIcon className="size-4 shrink-0 opacity-70" />
          <span className="truncate">{selected ? formatDay(selected) : placeholder}</span>
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-auto p-0">
        <Calendar
          mode="single"
          autoFocus
          captionLayout="dropdown"
          defaultMonth={selected}
          selected={selected}
          onSelect={(date) => {
            if (!date) return;
            onChange(toDateValue(date));
            setOpen(false);
          }}
        />
      </PopoverContent>
    </Popover>
  );
}

export function DateTimePicker({
  id,
  value,
  onChange,
  onBlur,
  placeholder = 'Pick a date',
  disabled,
  invalid,
  className,
}: {
  id?: string;
  /** "YYYY-MM-DDTHH:mm" in the reader's zone, or "" for no deadline. */
  value: string;
  onChange: (value: string) => void;
  onBlur?: () => void;
  placeholder?: string;
  disabled?: boolean;
  invalid?: boolean;
  className?: string;
}) {
  const [open, setOpen] = React.useState(false);
  const selected = parseValue(value);
  // A day without a time is not a deadline, so picking one keeps whatever time was
  // already set and falls back to end of day rather than to midnight — which would be
  // the day before the one the teacher just clicked.
  const time = selected ? toTimeValue(selected) : '23:59';

  function setDay(date: Date): void {
    onChange(`${toDateValue(date)}T${time}`);
  }

  function setTime(next: string): void {
    if (!next) return;
    const day = selected ? toDateValue(selected) : toDateValue(new Date());
    onChange(`${day}T${next}`);
  }

  return (
    <div className={cn('flex gap-2', className)}>
      <Popover
        open={open}
        onOpenChange={(next) => {
          setOpen(next);
          if (!next) onBlur?.();
        }}
      >
        <PopoverTrigger asChild>
          <Button
            id={id}
            type="button"
            variant="outline"
            disabled={disabled}
            aria-invalid={invalid}
            className={cn('min-w-0 flex-1 justify-start font-normal', !selected && 'text-muted-foreground')}
          >
            <CalendarIcon className="size-4 shrink-0 opacity-70" />
            <span className="truncate">{selected ? formatDay(selected) : placeholder}</span>
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-auto p-0">
          <Calendar
            mode="single"
            autoFocus
            captionLayout="dropdown"
            defaultMonth={selected}
            selected={selected}
            onSelect={(date) => {
              if (!date) return;
              setDay(date);
              setOpen(false);
            }}
          />
        </PopoverContent>
      </Popover>

      {/* The time stays a plain field: a clock in a popover would be three clicks for
          something a teacher types in one. */}
      <Label htmlFor={id ? `${id}-time` : undefined} className="sr-only">
        Time
      </Label>
      <Input
        id={id ? `${id}-time` : undefined}
        type="time"
        step={60}
        value={time}
        disabled={disabled}
        aria-invalid={invalid}
        onBlur={onBlur}
        onChange={(event) => setTime(event.target.value)}
        className="w-30 shrink-0 [&::-webkit-calendar-picker-indicator]:hidden"
      />
    </div>
  );
}
