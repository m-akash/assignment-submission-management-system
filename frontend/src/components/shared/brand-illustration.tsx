/**
 * The login hero illustration. It is an SVG rather than a stock photo on purpose:
 * it is theme-aware, razor-sharp at any size, and bundled — no network dependency
 * for the first screen a visitor sees. The motifs are drawn from the product itself:
 * an assignment card with a deadline, the graded submission that answers it, and a
 * mortarboard tying it back to the brand mark.
 *
 * The canvas is deliberately landscape (16:10) and scaled with `meet`, because the
 * slot it lives in is a wide, short band. A portrait canvas — or `slice` — would crop
 * the composition down to a middle sliver and read as a broken image.
 */
export function BrandIllustration({ className, ...props }: React.ComponentProps<'svg'>) {
  return (
    <svg
      viewBox="0 0 640 400"
      fill="none"
      className={className}
      preserveAspectRatio="xMidYMid meet"
      aria-hidden
      {...props}
    >
      <defs>
        {/* One shadow, reused: consistent light direction keeps the stack readable. */}
        <filter id="brand-lift" x="-25%" y="-25%" width="150%" height="150%">
          <feDropShadow dx="0" dy="16" stdDeviation="20" floodColor="#1e1b4b" floodOpacity="0.35" />
        </filter>
        <linearGradient id="brand-face" x1="0" y1="0" x2="0.4" y2="1">
          <stop offset="0" stopColor="#ffffff" />
          <stop offset="1" stopColor="#f6f5ff" />
        </linearGradient>
      </defs>

      {/* Sparse motes, for a little air around the object. */}
      <circle cx="238" cy="44" r="3.5" fill="#ffffff" opacity="0.3" />
      <circle cx="44" cy="336" r="4" fill="#ffffff" opacity="0.32" />
      <circle cx="614" cy="176" r="3" fill="#ffffff" opacity="0.28" />

      {/* A second card peeking out behind the first, so the hero reads as a stack. */}
      <rect x="88" y="52" width="286" height="200" rx="20" fill="#ffffff" opacity="0.22" />

      {/* ── The assignment ─────────────────────────────────────────── */}
      <g filter="url(#brand-lift)">
        <rect x="64" y="72" width="308" height="216" rx="20" fill="url(#brand-face)" />

        {/* course glyph */}
        <rect x="92" y="100" width="34" height="34" rx="11" fill="#eef2ff" />
        <rect x="101" y="109" width="16" height="3" rx="1.5" fill="#4f46e5" />
        <rect x="101" y="115.5" width="16" height="3" rx="1.5" fill="#818cf8" />
        <rect x="101" y="122" width="10" height="3" rx="1.5" fill="#c7d2fe" />

        {/* title + course line */}
        <rect x="138" y="104" width="140" height="12" rx="6" fill="#c7d2fe" />
        <rect x="138" y="124" width="88" height="8" rx="4" fill="#e4e8f0" />

        <rect x="92" y="154" width="252" height="1.5" rx="0.75" fill="#ebeef6" />

        {/* brief */}
        <rect x="92" y="174" width="224" height="9" rx="4.5" fill="#e2e8f0" />
        <rect x="92" y="194" width="190" height="9" rx="4.5" fill="#e2e8f0" />
        <rect x="92" y="214" width="152" height="9" rx="4.5" fill="#eaeef4" />

        {/* deadline pill */}
        <rect x="92" y="240" width="126" height="30" rx="15" fill="#fffbeb" />
        <circle cx="110" cy="255" r="8.5" fill="#f59e0b" />
        <path d="M110 251v4.5l3 2" stroke="#ffffff" strokeWidth="1.8" strokeLinecap="round" />
        <rect x="127" y="250" width="76" height="10" rx="5" fill="#fde68a" />
      </g>

      {/* ── The graded submission that answers it ──────────────────── */}
      <g filter="url(#brand-lift)">
        <rect x="336" y="200" width="272" height="168" rx="20" fill="url(#brand-face)" />

        {/* student */}
        <circle cx="372" cy="238" r="19" fill="#e0e7ff" />
        <circle cx="372" cy="232.5" r="6.5" fill="#6366f1" />
        <path d="M361.5 251a11 11 0 0 1 21 0z" fill="#6366f1" />

        <rect x="404" y="228" width="112" height="11" rx="5.5" fill="#c7d2fe" />
        <rect x="404" y="246" width="72" height="8" rx="4" fill="#e4e8f0" />

        {/* attachment chip */}
        <rect x="536" y="224" width="48" height="26" rx="13" fill="#f1f5f9" />
        <path
          d="M566 234l-8 8a4 4 0 0 1-6-6l7-7a2.6 2.6 0 0 1 4 4l-7 7"
          stroke="#94a3b8"
          strokeWidth="1.8"
          strokeLinecap="round"
        />
        <rect x="548" y="234" width="4" height="6" rx="2" fill="#cbd5e1" />

        <rect x="364" y="276" width="216" height="1.5" rx="0.75" fill="#ebeef6" />

        {/* the mark */}
        <circle cx="386" cy="316" r="21" fill="#10b981" />
        <path
          d="M377 316.5l6 6 12-13"
          stroke="#ffffff"
          strokeWidth="2.8"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        <rect x="420" y="304" width="104" height="13" rx="6.5" fill="#a7f3d0" />
        <rect x="420" y="326" width="68" height="9" rx="4.5" fill="#e4e8f0" />
      </g>

      {/* ── Mortarboard, balancing the diagonal ────────────────────── */}
      <g transform="translate(556 100)" filter="url(#brand-lift)">
        <path d="M0-22 46 0 0 22-46 0Z" fill="#ffffff" />
        <path d="M0-22 46 0 0 22Z" fill="#e0e7ff" opacity="0.75" />
        <path d="M-16 3Q0 22 16 3v10Q0 30-16 13Z" fill="#a5b4fc" />
        <circle cx="0" cy="0" r="3.5" fill="#4f46e5" />
        <path d="M0 0l28 11" stroke="#4f46e5" strokeWidth="2.2" strokeLinecap="round" />
        <circle cx="28" cy="18" r="4" fill="#fbbf24" />
      </g>
    </svg>
  );
}
