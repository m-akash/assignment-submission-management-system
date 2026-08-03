import axios, {
  AxiosError,
  AxiosResponse,
  InternalAxiosRequestConfig,
} from 'axios';
import { getAccessToken, setAccessToken } from './auth-token';
import type { ApiEnvelope, LoginResponse, ProblemDetails } from '@/types/api';

// Resolve base URL dynamically to handle Docker vs Local Dev port mapping automatically
let API_BASE_URL = process.env.NEXT_PUBLIC_API_URL;

if (!API_BASE_URL && typeof window !== 'undefined') {
  const isDockerCompose = window.location.port === '3000';
  const apiPort = isDockerCompose ? '5080' : '5269';
  API_BASE_URL = `${window.location.protocol}//${window.location.hostname}:${apiPort}`;
} else if (!API_BASE_URL) {
  API_BASE_URL = 'http://localhost:5269'; // Server-side render default
}

export const REFRESH_URL = '/api/v1/auth/refresh';
export const LOGIN_URL = '/api/v1/auth/login';
export const LOGOUT_URL = '/api/v1/auth/logout';

/** Endpoints that must never trigger a refresh-and-retry — doing so would recurse. */
const AUTH_ENDPOINTS = [REFRESH_URL, LOGIN_URL, LOGOUT_URL];

// `withCredentials` lets the httpOnly refresh cookie ride along. The cookie is
// scoped to /api/v1/auth on the server, so it is only actually sent to auth routes.
export const api = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
});

/**
 * Bare client for the refresh call: no interceptors, so a failing refresh can never
 * re-enter the refresh logic.
 */
const refreshClient = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
});

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

api.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  // Unwrap the success envelope so callers get `{ success, data, pagination }`.
  // Non-enveloped payloads (file downloads) pass through as the raw response.
  (response: AxiosResponse) =>
    response.data && response.data.success !== undefined ? response.data : response,

  async (error: AxiosError<ProblemDetails>) => {
    const config = error.config as RetriableRequestConfig | undefined;

    if (error.response?.status === 401 && config && !config._isRetry && !isAuthEndpoint(config.url)) {
      config._isRetry = true;
      try {
        await refreshAccessToken();
        return await api(config);
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

/**
 * Turns an axios failure into an `Error` carrying the server's message. Errors come
 * back as RFC 7807 ProblemDetails, so the useful text is in `detail` (falling back to
 * `title`) — not in the success envelope.
 */
function toApiError(error: AxiosError<ProblemDetails>): Error {
  const problem = error.response?.data;
  const message =
    (typeof problem === 'object' && (problem?.detail || problem?.title)) ||
    error.message ||
    'The request failed. Please try again.';

  return new Error(message);
}
