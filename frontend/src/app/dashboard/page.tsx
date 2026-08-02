'use client';

import React, { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/context/AuthContext';
import { GraduationCap, LogOut, User as UserIcon } from 'lucide-react';
import AdminDashboard from '@/components/AdminDashboard';
import TeacherDashboard from '@/components/TeacherDashboard';
import StudentDashboard from '@/components/StudentDashboard';

export default function DashboardPage() {
  const { user, logout, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && !user) {
      router.push('/login');
    }
  }, [user, loading, router]);

  if (loading || !user) {
    return (
      <div className="flex h-screen w-screen items-center justify-center bg-slate-950">
        <div className="flex items-center gap-3">
          <div className="h-6 w-6 border-2 border-indigo-500 border-t-transparent rounded-full animate-spin"></div>
          <span className="text-slate-400 text-sm font-medium">Authorizing session...</span>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col">
      {/* Top Navbar */}
      <header className="sticky top-0 z-40 bg-slate-900/60 backdrop-blur-xl border-b border-slate-800/80 px-6 py-4 shadow-lg shadow-indigo-500/[0.01]">
        <div className="max-w-7xl mx-auto flex justify-between items-center">
          {/* Logo */}
          <div className="flex items-center gap-2">
            <div className="h-9 w-9 bg-gradient-to-tr from-indigo-600 to-indigo-500 rounded-xl flex items-center justify-center shadow-md shadow-indigo-500/10">
              <GraduationCap className="h-5 w-5 text-slate-950" />
            </div>
            <div>
              <span className="text-lg font-bold text-white tracking-tight">Vanguard</span>
              <span className="text-[10px] text-indigo-400 block -mt-1 font-mono tracking-wider">ACADEMICS</span>
            </div>
          </div>

          {/* User profile & actions */}
          <div className="flex items-center gap-5">
            <div className="flex items-center gap-3 border-r border-slate-800/80 pr-5">
              <div className="h-8 w-8 rounded-lg bg-slate-800 flex items-center justify-center text-indigo-400 border border-slate-700/40">
                <UserIcon className="h-4 w-4" />
              </div>
              <div className="text-left">
                <p className="text-xs font-semibold text-slate-200">{user.fullName}</p>
                <div className="flex items-center gap-1.5 mt-0.5">
                  <span className={`px-2 py-0.5 rounded text-[9px] font-bold uppercase tracking-wider ${
                    user.role === 'Admin'
                      ? 'bg-purple-950 text-purple-300 border border-purple-800/50'
                      : user.role === 'Teacher'
                      ? 'bg-blue-950 text-blue-300 border border-blue-800/50'
                      : 'bg-emerald-950 text-emerald-300 border border-emerald-800/50'
                  }`}>
                    {user.role}
                  </span>
                  {user.className && (
                    <span className="text-[9px] font-mono text-slate-500">
                      Class: {user.className}
                    </span>
                  )}
                </div>
              </div>
            </div>

            <button
              onClick={logout}
              className="flex items-center gap-1.5 text-xs text-slate-400 hover:text-red-400 font-bold transition-colors py-1.5 px-3 rounded-lg hover:bg-slate-900/60"
            >
              <LogOut className="h-4 w-4" />
              Logout
            </button>
          </div>
        </div>
      </header>

      {/* Main Content Area */}
      <main className="flex-1 max-w-7xl w-full mx-auto p-6 md:p-8 flex flex-col">
        {user.role === 'Admin' && <AdminDashboard />}
        {user.role === 'Teacher' && <TeacherDashboard />}
        {user.role === 'Student' && <StudentDashboard />}
      </main>
    </div>
  );
}
