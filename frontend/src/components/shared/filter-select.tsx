'use client';

import { MultiCombobox, type ComboboxOption } from '@/components/ui/combobox';

export type Option = ComboboxOption;

/**
 * A list screen's filter control: searchable, and holding any number of values.
 *
 * An empty selection means "no filter" rather than "match nothing", which is why the
 * trigger reads "All classes" while it is empty. Every list endpoint accepts the
 * corresponding parameter repeated (`?classId=a&classId=b`), so several values narrow
 * to their union server-side, not just within the page already fetched.
 */
export function FilterSelect({
  values,
  onChange,
  options,
  allLabel,
  className = 'w-48',
  disabled,
}: {
  values: string[];
  onChange: (values: string[]) => void;
  options: Option[];
  /** Doubles as the empty-state label and the control's accessible name. */
  allLabel: string;
  className?: string;
  disabled?: boolean;
}) {
  // "All classes" → "Search classes…". Filters are always labelled that way, and a
  // hand-written placeholder per call site would only drift from it.
  const noun = allLabel.replace(/^all\s+/i, '');

  return (
    <MultiCombobox
      values={values}
      onChange={onChange}
      options={options}
      placeholder={allLabel}
      searchPlaceholder={`Search ${noun.toLowerCase()}…`}
      emptyMessage={`No ${noun.toLowerCase()} match`}
      aria-label={allLabel}
      className={className}
      disabled={disabled}
    />
  );
}
