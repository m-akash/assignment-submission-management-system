/**
 * The login hero illustration. It is an SVG rather than a stock photo on purpose:
 * it is theme-aware, razor-sharp at any size, and bundled — no network dependency
 * for the first screen a visitor sees. The motifs are drawn from the product itself:
 * a graded submission card (a check, a mark), a mortarboard, and a stack of books.
 */
export function BrandIllustration({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 520 620"
      fill="none"
      className={className}
      role="img"
      aria-label="Illustration of a graded submission, a graduation cap, and books"
      preserveAspectRatio="xMidYMid slice"
    >
      <defs>
        <filter id="brand-shadow" x="-20%" y="-20%" width="140%" height="140%">
          <feDropShadow dx="0" dy="14" stdDeviation="18" floodColor="#1e1b4b" floodOpacity="0.35" />
        </filter>
        <linearGradient id="brand-card" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#ffffff" />
          <stop offset="1" stopColor="#f5f3ff" />
        </linearGradient>
      </defs>

      {/* ── Atmosphere ─────────────────────────────────────────────── */}
      <circle cx="448" cy="78" r="132" fill="#ffffff" opacity="0.06" />
      <circle cx="448" cy="78" r="86" fill="#ffffff" opacity="0.05" />
      <circle cx="52" cy="556" r="116" fill="none" stroke="#ffffff" strokeWidth="1.5" opacity="0.12" />
      <circle cx="52" cy="556" r="70" fill="none" stroke="#ffffff" strokeWidth="1.5" opacity="0.1" />

      {[
        [40, 120],
        [486, 300],
        [70, 360],
        [470, 470],
        [300, 70],
      ].map(([x, y]) => (
        <circle key={`${x}-${y}`} cx={x} cy={y} r="3" fill="#ffffff" opacity="0.45" />
      ))}

      {/* ── Graded submission card (hero) ──────────────────────────── */}
      <g filter="url(#brand-shadow)" transform="rotate(-5 260 300)">
        <rect x="118" y="196" width="284" height="214" rx="20" fill="url(#brand-card)" />

        {/* course chip */}
        <circle cx="152" cy="232" r="16" fill="#eef2ff" />
        <path d="M146 232l4 4 8-9" stroke="#4f46e5" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" />

        {/* title + lines */}
        <rect x="180" y="226" width="150" height="11" rx="5.5" fill="#c7d2fe" />
        <rect x="152" y="268" width="216" height="9" rx="4.5" fill="#e2e8f0" />
        <rect x="152" y="290" width="184" height="9" rx="4.5" fill="#e2e8f0" />
        <rect x="152" y="312" width="160" height="9" rx="4.5" fill="#e2e8f0" />

        {/* graded pill */}
        <rect x="152" y="350" width="92" height="30" rx="15" fill="#ecfdf5" />
        <circle cx="168" cy="365" r="8" fill="#10b981" />
        <path d="M164 365l2.6 2.6L172 361" stroke="#ffffff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
        <rect x="184" y="360" width="46" height="10" rx="5" fill="#a7f3d0" />
      </g>

      {/* ── Graduation cap ─────────────────────────────────────────── */}
      <g transform="translate(118 470)" filter="url(#brand-shadow)">
        {/* mortarboard */}
        <path d="M0,-30 L62,0 L0,30 L-62,0 Z" fill="#ffffff" />
        <path d="M0,-30 L62,0 L0,30 Z" fill="#e0e7ff" opacity="0.7" />
        {/* cap crown beneath the board */}
        <path d="M-22,4 Q0,30 22,4 L22,18 Q0,40 -22,18 Z" fill="#a5b4fc" />
        {/* tassel button + cord */}
        <circle cx="0" cy="0" r="4.5" fill="#4f46e5" />
        <path d="M0,0 L40,16" stroke="#4f46e5" strokeWidth="2.5" strokeLinecap="round" />
        <circle cx="40" cy="24" r="4" fill="#fbbf24" />
      </g>

      {/* ── Books ──────────────────────────────────────────────────── */}
      <g transform="translate(360 458) rotate(8)" filter="url(#brand-shadow)">
        <rect x="-58" y="14" width="120" height="22" rx="6" fill="#ffffff" />
        <rect x="-50" y="-8" width="116" height="22" rx="6" fill="#a5b4fc" />
        <rect x="-44" y="-30" width="110" height="22" rx="6" fill="#fbbf24" />
        <rect x="-50" y="-2" width="6" height="10" rx="2" fill="#4f46e5" />
        <rect x="-44" y="-24" width="6" height="10" rx="2" fill="#4f46e5" />
      </g>
    </svg>
  );
}
