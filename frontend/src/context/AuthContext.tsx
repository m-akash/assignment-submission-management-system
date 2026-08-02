'use client';

import React, { createContext, useContext, useState, useEffect } from 'react';
import { api } from '@/lib/api';

interface User {
  id: string;
  fullName: string;
  email: string;
  role: 'Admin' | 'Teacher' | 'Student';
  classId?: string;
  className?: string;
}

interface AuthContextType {
  user: User | null;
  token: string | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<User>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Read from localStorage on mount
    const savedToken = localStorage.getItem('token');
    const savedUser = localStorage.getItem('user');
    if (savedToken && savedUser) {
      setToken(savedToken);
      setUser(JSON.parse(savedUser));
    }
    setLoading(false);
  }, []);

  const login = async (email: string, password: string): Promise<User> => {
    try {
      const res: any = await api.post('/api/v1/auth/login', { email, password });
      // res unpacked by axios interceptor will contain the { accessToken, id, email, fullName, role, classId, className }
      const data = res.data;
      const loggedUser: User = {
        id: data.id,
        fullName: data.fullName,
        email: data.email,
        role: data.role,
        classId: data.classId || undefined,
        className: data.className || undefined,
      };

      localStorage.setItem('token', data.accessToken);
      localStorage.setItem('user', JSON.stringify(loggedUser));
      setToken(data.accessToken);
      setUser(loggedUser);
      return loggedUser;
    } catch (err) {
      throw err;
    }
  };

  const logout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    setToken(null);
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, token, loading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
