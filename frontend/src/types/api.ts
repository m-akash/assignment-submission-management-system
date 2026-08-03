/** Shapes returned by the ASP.NET Core API. Mirrors the backend DTOs. */

export type Role = 'Admin' | 'Teacher' | 'Student';

/** Success envelope produced by `ApiResponse<T>` on the server. */
export interface ApiEnvelope<T> {
  success: boolean;
  data: T;
  message?: string | null;
  pagination?: PaginationMeta | null;
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

/** RFC 7807 body returned for every non-2xx response. */
export interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
  type?: string;
  instance?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

/** `UserDto` — also the payload of `GET /api/v1/auth/me`. */
export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  role: Role;
  classId: string | null;
  className: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

/** `AuthResponseBody` — the refresh token is never in the body, only in the cookie. */
export interface LoginResponse {
  userId: string;
  email: string;
  fullName: string;
  role: Role;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
}
