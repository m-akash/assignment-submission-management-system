/**
 * The login hero illustration. It is an SVG rather than a stock photo on purpose:
 * it is theme-aware, razor-sharp at any size, and bundled — no network dependency
 * for the first screen a visitor sees.
 *
 * The subject is a student in a mortarboard reading an open book at a desk, with the
 * two motifs of the product floating either side of them: the assignment that was set
 * (with its deadline) and the graded submission that answers it.
 *
 * The canvas is deliberately landscape (16:10) and scaled with `meet`, because the
 * slot it lives in is a wide, short band. A portrait canvas — or `slice` — would crop
 * the composition down to a middle sliver and read as a broken image.
 *
 * Draw order matters: the figure is laid down before the desk, so the desk slab hides
 * where the torso ends — that is what makes them read as sitting behind it. Stroke-only
 * paths carry an explicit `fill="none"`; without it they fill black wherever the root
 * `fill` is not inherited.
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

      {/* Sparse motes, for a little air around the figure. */}
      <circle cx="238" cy="40" r="3.5" fill="#ffffff" opacity="0.3" />
      <circle cx="46" cy="298" r="4" fill="#ffffff" opacity="0.32" />
      <circle cx="616" cy="284" r="3" fill="#ffffff" opacity="0.28" />

      {/* A halo behind the student, so the figure separates from the brand gradient. */}
      <circle cx="252" cy="198" r="152" fill="#ffffff" opacity="0.1" />

      {/* ── The assignment that was set ─────────────────────────────── */}
      <g filter="url(#brand-lift)">
        <rect x="34" y="130" width="158" height="98" rx="16" fill="url(#brand-face)" />

        {/* course glyph */}
        <rect x="56" y="150" width="26" height="26" rx="8" fill="#eef2ff" />
        <rect x="62" y="156" width="14" height="3" rx="1.5" fill="#4f46e5" />
        <rect x="62" y="162" width="14" height="3" rx="1.5" fill="#818cf8" />
        <rect x="62" y="168" width="9" height="3" rx="1.5" fill="#c7d2fe" />

        {/* title + course line */}
        <rect x="92" y="154" width="76" height="9" rx="4.5" fill="#c7d2fe" />
        <rect x="92" y="169" width="50" height="7" rx="3.5" fill="#e4e8f0" />

        {/* deadline pill */}
        <rect x="56" y="192" width="106" height="24" rx="12" fill="#fffbeb" />
        <circle cx="70" cy="204" r="7" fill="#f59e0b" />
        <path
          d="M70 200.5v3.9l2.6 1.7"
          stroke="#ffffff"
          strokeWidth="1.6"
          strokeLinecap="round"
          fill="none"
        />
        <rect x="84" y="200" width="62" height="8" rx="4" fill="#fde68a" />
      </g>

      {/* ── The graded submission that answers it ──────────────────── */}
      <g filter="url(#brand-lift)">
        <rect x="392" y="110" width="212" height="134" rx="18" fill="url(#brand-face)" />

        <rect x="416" y="138" width="104" height="11" rx="5.5" fill="#c7d2fe" />
        <rect x="416" y="157" width="68" height="8" rx="4" fill="#e4e8f0" />
        <rect x="416" y="182" width="164" height="1.5" rx="0.75" fill="#ebeef6" />

        {/* the mark */}
        <circle cx="436" cy="212" r="18" fill="#10b981" />
        <path
          d="M428.5 212.5l5 5 10-11"
          stroke="#ffffff"
          strokeWidth="2.6"
          strokeLinecap="round"
          strokeLinejoin="round"
          fill="none"
        />
        <rect x="466" y="202" width="92" height="12" rx="6" fill="#a7f3d0" />
        <rect x="466" y="222" width="60" height="8" rx="4" fill="#e4e8f0" />
      </g>

      {/* ── The student ─────────────────────────────────────────────── */}
      {/* neck first, so the head and torso both overlap it */}
      <rect x="239" y="174" width="22" height="30" rx="9" fill="#e3ae87" />

      {/* torso — runs past the desk line, which crops it cleanly */}
      <path d="M204 350v-104q0-38 46-38t46 38v104z" fill="#c7d2fe" />
      <path d="M234 210l16 19 16-19z" fill="#a5b4fc" />

      {/* head */}
      <circle cx="250" cy="150" r="36" fill="#f6cba9" />
      <path d="M214 147a36 36 0 0 1 72 0z" fill="#312e81" />
      <circle cx="238" cy="157" r="3.2" fill="#1e1b4b" />
      <circle cx="262" cy="157" r="3.2" fill="#1e1b4b" />
      <path
        d="M241 170q9 8 18 0"
        stroke="#1e1b4b"
        strokeWidth="2.6"
        strokeLinecap="round"
        fill="none"
      />

      {/* mortarboard — the same brand mark the header wears */}
      <g transform="translate(250 120)" filter="url(#brand-lift)">
        <path d="M0-16 44 0 0 16-44 0Z" fill="#ffffff" />
        <path d="M0-16 44 0 0 16Z" fill="#c7d2fe" opacity="0.8" />
        <circle cx="0" cy="0" r="3.5" fill="#4f46e5" />
        {/* the tassel hangs off the board's tip, clear of the face */}
        <path
          d="M0 0l47 2v26"
          stroke="#4f46e5"
          strokeWidth="2.2"
          strokeLinecap="round"
          fill="none"
        />
        <circle cx="47" cy="32" r="4.5" fill="#fbbf24" />
      </g>

      {/* arms, reaching down to the page corners they hold */}
      <path
        d="M216 246q-26 22-36 50"
        stroke="#a5b4fc"
        strokeWidth="19"
        strokeLinecap="round"
        fill="none"
      />
      <path
        d="M284 246q26 22 36 50"
        stroke="#a5b4fc"
        strokeWidth="19"
        strokeLinecap="round"
        fill="none"
      />

      {/* the open book */}
      <path d="M250 276 172 266v46l78 10z" fill="#4f46e5" />
      <path d="M250 276 328 266v46l-78 10z" fill="#4338ca" />
      <path d="M250 270 180 261v44l70 9z" fill="url(#brand-face)" />
      <path d="M250 270 320 261v44l-70 9z" fill="url(#brand-face)" />
      <path d="M250 270v44" stroke="#c7d2fe" strokeWidth="2.5" strokeLinecap="round" fill="none" />
      <g stroke="#c7d2fe" strokeWidth="3" strokeLinecap="round" fill="none">
        <path d="M192 275l48 6" />
        <path d="M192 285l48 6" />
        <path d="M192 295l40 5" />
        <path d="M308 275l-48 6" />
        <path d="M308 285l-48 6" />
        <path d="M308 295l-40 5" />
      </g>

      {/* hands, drawn last so they sit on top of the page they grip */}
      <circle cx="179" cy="299" r="10" fill="#f6cba9" />
      <circle cx="321" cy="299" r="10" fill="#f6cba9" />

      {/* ── The desk, and what sits on it ──────────────────────────── */}
      <rect x="48" y="330" width="544" height="20" rx="10" fill="#ffffff" opacity="0.95" />
      <rect x="96" y="350" width="448" height="7" rx="3.5" fill="#1e1b4b" opacity="0.14" />

      {/* a stack of books, and a pot of pencils */}
      <rect x="86" y="318" width="80" height="14" rx="5" fill="#a5b4fc" />
      <rect x="94" y="304" width="72" height="14" rx="5" fill="#ffffff" opacity="0.92" />
      <rect x="88" y="290" width="76" height="14" rx="5" fill="#fbbf24" />

      <rect x="478" y="282" width="6" height="26" rx="3" fill="#fbbf24" />
      <rect x="490" y="276" width="6" height="32" rx="3" fill="#34d399" />
      <rect x="468" y="304" width="36" height="28" rx="8" fill="#e0e7ff" />
    </svg>
  );
}
