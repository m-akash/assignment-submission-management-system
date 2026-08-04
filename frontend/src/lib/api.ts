import axios, {
  AxiosError,
  AxiosRequestConfig,
  InternalAxiosRequestConfig,
} from 'axios';
import { getAccessToken, setAccessToken } from './auth-token';
import type { ApiEnvelope, LoginResponse, Paged, PaginationMeta, ProblemDetails } from '@/types/api';

// Defaults to the local `dotnet run` port (5269, from launchSettings.json). When the
// stack runs under Docker Compose the API is on 5080 — set NEXT_PUBLIC_API_URL to
// override, which the web service's build args already do.
const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, '') ?? 'http://localhost:5269';

export const REFRESH_URL = '/api/v1/auth/refresh';
export const LOGIN_URL = '/api/v1/auth/login';
export const LOGOUT_URL = '/api/v1/auth/logout';
export const ME_URL = '/api/v1/auth/me';

/** Endpoints that must never trigger a refresh-and-retry — doing so would recurse. */
const AUTH_ENDPOINTS = [REFRESH_URL, LOGIN_URL, LOGOUT_URL];

// `withCredentials` lets the httpOnly refresh cookie ride along. The cookie is scoped
// to the root path server-side (see AuthConstants.cs) so the Next.js proxy can also
// read its presence on document requests, not just on calls to this client.
const client = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' },
});

/**
 * Bare client for the refresh call: no interceptors, so a failing refresh can never
 * re-enter the refresh logic.
 */
const refreshClient = axios.create({ baseURL: API_BASE_URL, withCredentials: true });

let refreshInFlight: Promise<string> | null = null;

/**
 * Exchanges the httpOnly refresh cookie for a new access token.
 *
 * Single-flight on purpose. Refresh tokens rotate — presenting an already-rotated
 * token trips the server's reuse detection and revokes the whole token family. A
 * page that fires several requests at once would do exactly that without this guard,
 * so concurrent callers share one in-flight request.
 */
export function refreshAccessToken(): Promise<string> {
  refreshInFlight ??= refreshClient
    .post<ApiEnvelope<LoginResponse>>(REFRESH_URL)
    .then((response) => {
      const token = response.data?.data?.accessToken;
      if (!token) {
        throw new Error('Refresh response did not contain an access token.');
      }
      setAccessToken(token);
      return token;
    })
    .finally(() => {
      refreshInFlight = null;
    });

  return refreshInFlight;
}

client.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

client.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ProblemDetails>) => {
    const config = error.config as RetriableRequestConfig | undefined;

    if (error.response?.status === 401 && config && !config._isRetry && !isAuthEndpoint(config.url)) {
      config._isRetry = true;
      try {
        await refreshAccessToken();
        return await client(config);
      } catch {
        // The refresh cookie is gone or revoked — the session is genuinely over.
        setAccessToken(null);
        redirectToLogin();
      }
    }

    return Promise.reject(toApiError(error));
  },
);

interface RetriableRequestConfig extends InternalAxiosRequestConfig {
  _isRetry?: boolean;
}

function isAuthEndpoint(url: string | undefined): boolean {
  return !!url && AUTH_ENDPOINTS.some((endpoint) => url.startsWith(endpoint));
}

function redirectToLogin(): void {
  if (typeof window !== 'undefined' && window.location.pathname !== '/login') {
    window.location.href = '/login';
  }
}

/** Carries the server's error code so callers can branch on it without parsing text. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status?: number,
    readonly code?: string,
    readonly fieldErrors?: Record<string, string[]>,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

/**
 * Turns an axios failure into an `ApiError` carrying the server's message. Errors come
 * back as RFC 7807 ProblemDetails, so the useful text is in `detail` (falling back to
 * `title`) — not in the success envelope.
 */
function toApiError(error: AxiosError<ProblemDetails>): ApiError {
  const problem = error.response?.data;
  const message =
    (typeof problem === 'object' && (problem?.detail || problem?.title)) ||
    error.message ||
    'The request failed. Please try again.';

  return new ApiError(message, error.response?.status, problem?.code, problem?.errors);
}

// ── Typed request helpers ───────────────────────────────────────────────────
// Every endpoint answers with the same envelope, so callers get `T` directly rather
// than reaching through `response.data.data` at each call site.

export async function apiGet<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
  const response = await client.get<ApiEnvelope<T>>(url, config);
  return response.data.data;
}

/** For list endpoints: returns the items alongside their pagination metadata. */
export async function apiGetPaged<T>(url: string, config?: AxiosRequestConfig): Promise<Paged<T>> {
  const response = await client.get<ApiEnvelope<T[]>>(url, config);
  return {
    items: response.data.data ?? [],
    pagination: response.data.pagination ?? emptyPagination,
  };
}

export async function apiPost<T>(url: string, body?: unknown, config?: AxiosRequestConfig): Promise<T> {
  const response = await client.post<ApiEnvelope<T>>(url, body, config);
  return response.data?.data;
}

export async function apiPut<T>(url: string, body?: unknown): Promise<T> {
  const response = await client.put<ApiEnvelope<T>>(url, body);
  return response.data?.data;
}

export async function apiDelete(url: string): Promise<void> {
  await client.delete(url);
}

/** Downloads a file as a Blob, bypassing the envelope entirely. */
export async function apiGetBlob(url: string): Promise<Blob> {
  const response = await client.get<Blob>(url, { responseType: 'blob' });
  return response.data;
}

export async function apiPostForm<T>(url: string, form: FormData): Promise<T> {
  const response = await client.post<ApiEnvelope<T>>(url, form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return response.data?.data;
}

const emptyPagination: PaginationMeta = { page: 1, pageSize: 0, total: 0, totalPages: 0 };

/** Drops empty filter values so they never reach the query string. */
export function toQuery(params: Record<string, string | number | boolean | null | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') {
      search.set(key, String(value));
    }
  }
  const query = search.toString();
  return query ? `?${query}` : '';
}
