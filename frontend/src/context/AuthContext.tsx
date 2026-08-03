'use client';

import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { api, LOGIN_URL, LOGOUT_URL, refreshAccessToken } from '@/lib/api';
import { setAccessToken } from '@/lib/auth-token';
import type { ApiEnvelope, AuthUser, LoginResponse } from '@/types/api';

interface AuthContextValue {
  user: AuthUser | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<AuthUser>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

const ME_URL = '/api/v1/auth/me';

/** `GET /auth/me` — the only source of the signed-in identity. */
async function fetchProfile(): Promise<AuthUser> {
  const envelope = (await api.get(ME_URL)) as unknown as ApiEnvelope<AuthUser>;
  return envelope.data;
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  // Restore the session on mount. The access token is deliberately not persisted,
  // so we trade the httpOnly refresh cookie for a fresh one and re-fetch the profile.
  // A failure here just means "not signed in" — it is the expected path for a visitor.
  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        await refreshAccessToken();
        const profile = await fetchProfile();
        if (!cancelled) setUser(profile);
      } catch {
        if (!cancelled) {
          setAccessToken(null);
          setUser(null);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const login = useCallback(async (email: string, password: string): Promise<AuthUser> => {
    const envelope = (await api.post(LOGIN_URL, { email, password })) as unknown as ApiEnvelope<LoginResponse>;
    setAccessToken(envelope.data.accessToken);

    const profile = await fetchProfile();
    setUser(profile);
    return profile;
  }, []);

  const logout = useCallback(async (): Promise<void> => {
    // Best effort: revoke the refresh token server-side, but always clear locally.
    try {
      await api.post(LOGOUT_URL);
    } catch {
      // An already-expired session is still a successful logout from here.
    }

    setAccessToken(null);
    setUser(null);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ user, loading, login, logout }),
    [user, loading, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
