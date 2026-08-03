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

const PUBLIC_PATHS = ['/login'];

export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const hasSession = request.cookies.has(REFRESH_COOKIE);
  const isPublic = PUBLIC_PATHS.some((path) => pathname.startsWith(path));

  if (!hasSession && !isPublic) {
    const login = new URL('/login', request.url);
    // Remember where they were headed so login can return them there.
    if (pathname !== '/') {
      login.searchParams.set('next', pathname);
    }
    return NextResponse.redirect(login);
  }

  if (hasSession && isPublic) {
    return NextResponse.redirect(new URL('/', request.url));
  }

  return NextResponse.next();
}

export const config = {
  // Skip Next internals and static assets.
  matcher: ['/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp|ico)$).*)'],
};
