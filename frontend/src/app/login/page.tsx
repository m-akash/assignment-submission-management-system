'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/context/AuthContext';
import { GraduationCap, Lock, Mail, AlertCircle, Eye, EyeOff } from 'lucide-react';

export default function LoginPage() {
  const { user, login, loading: authLoading } = useAuth();
  const router = useRouter();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (user && !authLoading) {
      router.push('/dashboard');
    }
  }, [user, authLoading, router]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email || !password) {
      setError('Please fill in all fields.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await login(email, password);
      router.push('/dashboard');
    } catch (err: any) {
      setError(err.message || 'Invalid email or password.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-950 p-4 relative overflow-hidden">
      {/* Background glowing decorations */}
      <div className="absolute top-1/4 left-1/4 -translate-x-1/2 -translate-y-1/2 w-96 h-96 bg-indigo-500/10 rounded-full blur-[100px] pointer-events-none"></div>
      <div className="absolute bottom-1/4 right-1/4 translate-x-1/2 translate-y-1/2 w-96 h-96 bg-emerald-500/10 rounded-full blur-[100px] pointer-events-none"></div>

      <div className="w-full max-w-md">
        {/* Logo and header */}
        <div className="flex flex-col items-center mb-8 text-center animate-fade-in">
          <div className="h-16 w-16 bg-gradient-to-tr from-indigo-600 to-indigo-400 rounded-2xl flex items-center justify-center shadow-lg shadow-indigo-500/20 mb-4 transform hover:scale-105 transition-transform duration-300">
            <GraduationCap className="h-9 w-9 text-slate-950" strokeWidth={2} />
          </div>
          <h1 className="text-3xl font-extrabold tracking-tight text-white sm:text-4xl bg-gradient-to-r from-slate-100 to-slate-400 bg-clip-text text-transparent">
            Vanguard Portal
          </h1>
          <p className="mt-2 text-sm text-slate-400">
            Enter your credentials to access your assignments
          </p>
        </div>

        {/* Login card */}
        <div className="bg-slate-900/60 backdrop-blur-xl border border-slate-800 rounded-3xl p-8 shadow-2xl relative overflow-hidden">
          {/* Subtle top border gradient line */}
          <div className="absolute top-0 left-0 right-0 h-[2px] bg-gradient-to-r from-indigo-500 via-purple-500 to-emerald-500"></div>

          {error && (
            <div className="mb-6 flex items-start gap-3 rounded-xl bg-red-950/50 border border-red-800/40 p-4 text-sm text-red-200">
              <AlertCircle className="h-5 w-5 text-red-400 shrink-0 mt-0.5" />
              <div>
                <p className="font-semibold">Authentication failed</p>
                <p className="text-red-300/90 mt-0.5">{error}</p>
              </div>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-6">
            {/* Email Field */}
            <div>
              <label htmlFor="email" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
                Email Address
              </label>
              <div className="relative rounded-xl shadow-sm">
                <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-4">
                  <Mail className="h-5 w-5 text-slate-500" />
                </div>
                <input
                  type="email"
                  name="email"
                  id="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="block w-full rounded-xl border border-slate-800 bg-slate-950/60 py-3 pl-11 pr-4 text-slate-200 placeholder-slate-500 focus:border-indigo-500 focus:bg-slate-950 focus:ring-1 focus:ring-indigo-500 outline-none transition-all duration-200 sm:text-sm"
                  placeholder="name@school.com"
                  required
                />
              </div>
            </div>

            {/* Password Field */}
            <div>
              <div className="flex justify-between items-center mb-2">
                <label htmlFor="password" className="block text-xs font-semibold uppercase tracking-wider text-slate-400">
                  Password
                </label>
              </div>
              <div className="relative rounded-xl shadow-sm">
                <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-4">
                  <Lock className="h-5 w-5 text-slate-500" />
                </div>
                <input
                  type={showPassword ? 'text' : 'password'}
                  name="password"
                  id="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="block w-full rounded-xl border border-slate-800 bg-slate-950/60 py-3 pl-11 pr-12 text-slate-200 placeholder-slate-500 focus:border-indigo-500 focus:bg-slate-950 focus:ring-1 focus:ring-indigo-500 outline-none transition-all duration-200 sm:text-sm"
                  placeholder="••••••••"
                  required
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute inset-y-0 right-0 flex items-center pr-4 text-slate-500 hover:text-slate-300 focus:outline-none"
                >
                  {showPassword ? <EyeOff className="h-5 w-5" /> : <Eye className="h-5 w-5" />}
                </button>
              </div>
            </div>

            {/* Submit Button */}
            <button
              type="submit"
              disabled={loading}
              className="w-full flex items-center justify-center py-3.5 px-4 border border-transparent rounded-xl shadow-md text-sm font-bold text-slate-950 bg-gradient-to-r from-indigo-400 to-indigo-300 hover:from-indigo-300 hover:to-indigo-200 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 focus:ring-offset-slate-950 transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed transform active:scale-[0.98]"
            >
              {loading ? (
                <div className="h-5 w-5 border-2 border-slate-950 border-t-transparent rounded-full animate-spin"></div>
              ) : (
                'Sign In'
              )}
            </button>
          </form>

          {/* Quick instructions/demo credentials */}
          <div className="mt-8 pt-6 border-t border-slate-800/80 text-center">
            <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider block mb-3">Demo Credentials</span>
            <div className="grid grid-cols-1 gap-2 text-xs text-left max-w-xs mx-auto text-slate-400 bg-slate-950/40 p-3 rounded-xl border border-slate-800/40">
              <div><span className="font-semibold text-slate-300">Admin:</span> admin@assignment.test</div>
              <div><span className="font-semibold text-slate-300">Teacher:</span> teacher@assignment.test</div>
              <div><span className="font-semibold text-slate-300">Student:</span> student@assignment.test</div>
              <div className="border-t border-slate-800/60 mt-1 pt-1 text-[10px] text-slate-500 text-center">Password: <code className="bg-slate-900 px-1 rounded text-slate-400">Password123!</code> for all</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
