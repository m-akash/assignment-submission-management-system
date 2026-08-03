/**
 * The access token is held in memory only — never in localStorage or a readable
 * cookie, so an XSS payload cannot exfiltrate it. It is lost on page reload by
 * design: the session is restored from the httpOnly refresh cookie instead
 * (see `refreshAccessToken` in `lib/api.ts`).
 */
let accessToken: string | null = null;

export function getAccessToken(): string | null {
  return accessToken;
}

export function setAccessToken(token: string | null): void {
  accessToken = token;
}
