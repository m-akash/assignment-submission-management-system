'use client';

import { useRef } from 'react';
import { XIcon } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { cn } from '@/lib/utils';

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
  const triggerRef = useRef<HTMLButtonElement>(null);
  const showClear = Boolean(value) && !disabled;

  return (
    <div className={cn('relative', className)}>
      <Select
        value={value || ALL}
        onValueChange={(next) => onChange(next === ALL ? '' : next)}
        disabled={disabled}
      >
        {/* While a filter is applied the chevron is hidden and the clear button takes its place. */}
        <SelectTrigger
          ref={triggerRef}
          className={cn('w-full', showClear && '[&>svg]:invisible')}
          aria-label={allLabel}
        >
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
      {showClear ? (
        <button
          type="button"
          aria-label={`Clear filter — show ${allLabel.toLowerCase()}`}
          title={`Clear filter — show ${allLabel.toLowerCase()}`}
          onClick={() => {
            onChange('');
            triggerRef.current?.focus();
          }}
          className="absolute top-1/2 right-2 flex size-4 -translate-y-1/2 items-center justify-center rounded-sm text-muted-foreground transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:outline-none"
        >
          <XIcon className="size-3.5" />
        </button>
      ) : null}
    </div>
  );
}
