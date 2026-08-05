import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

/**
 * Route gate. (Next.js 16 renamed Middleware to Proxy; the convention is a single
 * `proxy.ts` beside `app`.)
 *
 * This is an optimistic check on the presence of the refresh cookie, not an
 * authorization decision — the cookie is httpOnly and its contents are never inspected
 * here. It keeps signed-out visitors off the dashboard shell and signed-in ones off the
 * login page. Every actual permission is enforced by the API, which is the only place
 * that can be trusted.
 */
const REFRESH_COOKIE = 'asm_refresh';

const PUBLIC_PATHS = ['/login', '/set-password'];

/**
 * Public paths a signed-in visitor is still allowed to reach. `/login` bounces them to
 * the dashboard because they are already there; `/set-password` must not, since the link
 * belongs to whoever's mailbox it arrived in — which may not be the account whose stale
 * cookie happens to be in this browser.
 */
const PUBLIC_PATHS_ALLOWING_SESSION = ['/set-password'];

export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const hasSession = request.cookies.has(REFRESH_COOKIE);
  const isPublic = PUBLIC_PATHS.some((path) => pathname.startsWith(path));
  const allowsSession = PUBLIC_PATHS_ALLOWING_SESSION.some((path) => pathname.startsWith(path));

  if (!hasSession && !isPublic) {
    const login = new URL('/login', request.url);
    // Remember where they were headed so login can return them there.
    if (pathname !== '/') {
      login.searchParams.set('next', pathname);
    }
    return NextResponse.redirect(login);
  }

  if (hasSession && isPublic && !allowsSession) {
    return NextResponse.redirect(new URL('/', request.url));
  }

  return NextResponse.next();
}

export const config = {
  // Skip Next internals and static assets.
  matcher: ['/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp|ico)$).*)'],
};
