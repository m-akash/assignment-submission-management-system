'use client';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

export interface Option {
  value: string;
  label: string;
}

/** Sentinel for "no filter" — Radix Select cannot hold an empty string value. */
const ALL = '__all__';

export function FilterSelect({
  value,
  onChange,
  options,
  allLabel,
  className = 'w-[170px]',
  disabled,
}: {
  value: string;
  onChange: (value: string) => void;
  options: Option[];
  allLabel: string;
  className?: string;
  disabled?: boolean;
}) {
  return (
    <Select
      value={value || ALL}
      onValueChange={(next) => onChange(next === ALL ? '' : next)}
      disabled={disabled}
    >
      <SelectTrigger className={className} aria-label={allLabel}>
        <SelectValue placeholder={allLabel} />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={ALL}>{allLabel}</SelectItem>
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
