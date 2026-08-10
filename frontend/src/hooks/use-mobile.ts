import * as React from 'react';

const MOBILE_BREAKPOINT = 768;

/**
 * Whether the viewport is narrow enough that the sidebar should be a sheet.
 *
 * shadcn ships this as a `useState` seeded from an effect. The media query is an external
 * store, though, and this project's lint rules say so: `useSyncExternalStore` subscribes
 * to it directly, which is one render instead of two and returns `false` during SSR
 * rather than a first paint that has to be corrected.
 */
function subscribe(onChange: () => void): () => void {
  const query = window.matchMedia(`(max-width: ${MOBILE_BREAKPOINT - 1}px)`);
  query.addEventListener('change', onChange);
  return () => query.removeEventListener('change', onChange);
}

export function useIsMobile(): boolean {
  return React.useSyncExternalStore(
    subscribe,
    () => window.innerWidth < MOBILE_BREAKPOINT,
    // The server has no viewport; the desktop layout is the one that degrades better if
    // the first client render disagrees.
    () => false,
  );
}
