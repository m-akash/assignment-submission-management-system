'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { api } from '@/lib/api';
import { 
  Users, BookOpen, Layers, Link as LinkIcon, Plus, Search, 
  Trash2, Edit, ChevronLeft, ChevronRight, UserPlus, FolderPlus,
  BookPlus, X, RefreshCw
} from 'lucide-react';

export default function AdminDashboard() {
  const [activeTab, setActiveTab] = useState<'users' | 'classes' | 'subjects' | 'mappings'>('users');

  return (
    <div className="flex-1 flex flex-col gap-6">
      {/* Tab Selector */}
      <div className="flex border-b border-slate-800 bg-slate-900/40 p-1.5 rounded-xl self-start">
        <button
          onClick={() => setActiveTab('users')}
          className={`flex items-center gap-2 px-4 py-2 text-sm font-semibold rounded-lg transition-all duration-200 ${
            activeTab === 'users'
              ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-500/10'
              : 'text-slate-400 hover:text-slate-200'
          }`}
        >
          <Users className="h-4 w-4" />
          Users
        </button>
        <button
          onClick={() => setActiveTab('classes')}
          className={`flex items-center gap-2 px-4 py-2 text-sm font-semibold rounded-lg transition-all duration-200 ${
            activeTab === 'classes'
              ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-500/10'
              : 'text-slate-400 hover:text-slate-200'
          }`}
        >
          <Layers className="h-4 w-4" />
          Classes
        </button>
        <button
          onClick={() => setActiveTab('subjects')}
          className={`flex items-center gap-2 px-4 py-2 text-sm font-semibold rounded-lg transition-all duration-200 ${
            activeTab === 'subjects'
              ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-500/10'
              : 'text-slate-400 hover:text-slate-200'
          }`}
        >
          <BookOpen className="h-4 w-4" />
          Subjects
        </button>
        <button
          onClick={() => setActiveTab('mappings')}
          className={`flex items-center gap-2 px-4 py-2 text-sm font-semibold rounded-lg transition-all duration-200 ${
            activeTab === 'mappings'
              ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-500/10'
              : 'text-slate-400 hover:text-slate-200'
          }`}
        >
          <LinkIcon className="h-4 w-4" />
          Teacher Mappings
        </button>
      </div>

      {/* Tab Panels */}
      <div className="flex-1 flex flex-col">
        {activeTab === 'users' && <UsersPanel />}
        {activeTab === 'classes' && <ClassesPanel />}
        {activeTab === 'subjects' && <SubjectsPanel />}
        {activeTab === 'mappings' && <MappingsPanel />}
      </div>
    </div>
  );
}

// ── USERS PANEL ─────────────────────────────────────────────────────────────
function UsersPanel() {
  const [users, setUsers] = useState<any[]>([]);
  const [classes, setClasses] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState('');
  const [classFilter, setClassFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  // Form states
  const [showModal, setShowModal] = useState(false);
  const [editingUser, setEditingUser] = useState<any | null>(null);
  const [formEmail, setFormEmail] = useState('');
  const [formFullName, setFormFullName] = useState('');
  const [formRole, setFormRole] = useState<'Admin' | 'Teacher' | 'Student'>('Student');
  const [formClassId, setFormClassId] = useState('');
  const [formPassword, setFormPassword] = useState('');
  const [error, setError] = useState<string | null>(null);

  const fetchUsers = useCallback(async () => {
    setLoading(true);
    try {
      const url = `/api/v1/users?page=${page}&pageSize=10&search=${encodeURIComponent(search)}${
        roleFilter ? `&role=${roleFilter}` : ''
      }${classFilter ? `&classId=${classFilter}` : ''}`;
      const res: any = await api.get(url);
      setUsers(res.data || []);
      setTotalPages(res.pagination?.totalPages || 1);
    } catch (err: any) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, [page, search, roleFilter, classFilter]);

  const fetchClasses = async () => {
    try {
      const res: any = await api.get('/api/v1/classes?pageSize=100');
      setClasses(res.data || []);
    } catch (err) {
      console.error(err);
    }
  };

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  useEffect(() => {
    fetchClasses();
  }, []);

  const openCreateModal = () => {
    setEditingUser(null);
    setFormEmail('');
    setFormFullName('');
    setFormRole('Student');
    setFormClassId('');
    setFormPassword('');
    setError(null);
    setShowModal(true);
  };

  const openEditModal = (user: any) => {
    setEditingUser(user);
    setFormEmail(user.email);
    setFormFullName(user.fullName);
    setFormRole(user.role);
    setFormClassId(user.classId || '');
    setFormPassword(''); // blank for no password update
    setError(null);
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      if (editingUser) {
        // Update user
        await api.put(`/api/v1/users/${editingUser.id}`, {
          fullName: formFullName,
          email: formEmail,
          role: formRole,
          classId: formRole === 'Student' && formClassId ? formClassId : null,
          password: formPassword || null
        });
      } else {
        // Create user
        if (!formPassword) {
          setError('Password is required for new users.');
          return;
        }
        await api.post('/api/v1/users', {
          fullName: formFullName,
          email: formEmail,
          role: formRole,
          classId: formRole === 'Student' && formClassId ? formClassId : null,
          password: formPassword
        });
      }
      setShowModal(false);
      fetchUsers();
    } catch (err: any) {
      setError(err.message || 'Failed to save user.');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this user?')) return;
    try {
      await api.delete(`/api/v1/users/${id}`);
      fetchUsers();
    } catch (err: any) {
      alert(err.message || 'Failed to delete user.');
    }
  };

  return (
    <div className="bg-slate-900/40 border border-slate-800 rounded-2xl p-6 flex flex-col gap-6 backdrop-blur-xl">
      {/* Header Controls */}
      <div className="flex flex-wrap gap-4 items-center justify-between">
        <div className="flex flex-wrap gap-3 items-center flex-1 max-w-2xl">
          <div className="relative flex-1 min-w-[200px]">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-500" />
            <input
              type="text"
              placeholder="Search users..."
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
              className="w-full bg-slate-950/60 border border-slate-800 rounded-xl pl-10 pr-4 py-2 text-sm text-slate-200 outline-none focus:border-indigo-500 transition-colors duration-150"
            />
          </div>
          <select
            value={roleFilter}
            onChange={(e) => { setRoleFilter(e.target.value); setPage(1); }}
            className="bg-slate-950/60 border border-slate-800 rounded-xl px-3 py-2 text-sm text-slate-400 outline-none focus:border-indigo-500"
          >
            <option value="">All Roles</option>
            <option value="Admin">Admin</option>
            <option value="Teacher">Teacher</option>
            <option value="Student">Student</option>
          </select>
          <select
            value={classFilter}
            onChange={(e) => { setClassFilter(e.target.value); setPage(1); }}
            className="bg-slate-950/60 border border-slate-800 rounded-xl px-3 py-2 text-sm text-slate-400 outline-none focus:border-indigo-500"
          >
            <option value="">All Classes</option>
            {classes.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name} ({c.grade}-{c.section})
              </option>
            ))}
          </select>
        </div>

        <button
          onClick={openCreateModal}
          className="flex items-center gap-2 bg-gradient-to-r from-indigo-500 to-indigo-400 hover:from-indigo-400 hover:to-indigo-300 text-slate-950 px-4 py-2 rounded-xl text-sm font-bold shadow-lg shadow-indigo-500/10 transition-all duration-150"
        >
          <UserPlus className="h-4 w-4" />
          Create User
        </button>
      </div>

      {/* Table */}
      <div className="overflow-x-auto border border-slate-800/60 rounded-xl">
        <table className="w-full text-left text-sm text-slate-300">
          <thead className="bg-slate-950/40 text-slate-400 uppercase tracking-wider text-xs font-semibold">
            <tr>
              <th className="px-6 py-4">Full Name</th>
              <th className="px-6 py-4">Email</th>
              <th className="px-6 py-4">Role</th>
              <th className="px-6 py-4">Class</th>
              <th className="px-6 py-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50 bg-slate-900/10">
            {loading ? (
              <tr>
                <td colSpan={5} className="text-center py-8">
                  <div className="flex justify-center items-center gap-2">
                    <RefreshCw className="h-4 w-4 animate-spin text-indigo-500" />
                    <span>Loading users...</span>
                  </div>
                </td>
              </tr>
            ) : users.length === 0 ? (
              <tr>
                <td colSpan={5} className="text-center py-8 text-slate-500">
                  No users found matching your filters.
                </td>
              </tr>
            ) : (
              users.map((u) => (
                <tr key={u.id} className="hover:bg-slate-800/20 transition-colors">
                  <td className="px-6 py-4 font-semibold text-white">{u.fullName}</td>
                  <td className="px-6 py-4 text-slate-400">{u.email}</td>
                  <td className="px-6 py-4">
                    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border ${
                      u.role === 'Admin' 
                        ? 'bg-purple-950/40 border-purple-800 text-purple-300' 
                        : u.role === 'Teacher' 
                        ? 'bg-blue-950/40 border-blue-800 text-blue-300' 
                        : 'bg-emerald-950/40 border-emerald-800 text-emerald-300'
                    }`}>
                      {u.role}
                    </span>
                  </td>
                  <td className="px-6 py-4 text-slate-400">{u.className || '-'}</td>
                  <td className="px-6 py-4 text-right">
                    <div className="flex gap-2 justify-end">
                      <button
                        onClick={() => openEditModal(u)}
                        className="p-1.5 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-slate-200 transition-all"
                      >
                        <Edit className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => handleDelete(u.id)}
                        className="p-1.5 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-red-400 transition-all"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      <div className="flex justify-between items-center text-sm text-slate-400">
        <div>Page {page} of {totalPages}</div>
        <div className="flex gap-2">
          <button
            disabled={page <= 1}
            onClick={() => setPage(page - 1)}
            className="p-2 border border-slate-800 rounded-lg hover:bg-slate-800/40 disabled:opacity-40 disabled:cursor-not-allowed transition-all"
          >
            <ChevronLeft className="h-4 w-4" />
          </button>
          <button
            disabled={page >= totalPages}
            onClick={() => setPage(page + 1)}
            className="p-2 border border-slate-800 rounded-lg hover:bg-slate-800/40 disabled:opacity-40 disabled:cursor-not-allowed transition-all"
          >
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>
      </div>

      {/* User Form Modal */}
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="w-full max-w-md bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden shadow-2xl relative">
            <div className="absolute top-0 left-0 right-0 h-[2px] bg-indigo-500"></div>
            <div className="p-6 flex items-center justify-between border-b border-slate-800">
              <h3 className="font-bold text-white text-lg">{editingUser ? 'Edit User' : 'Create User'}</h3>
              <button onClick={() => setShowModal(false)} className="text-slate-400 hover:text-white transition-all">
                <X className="h-5 w-5" />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="p-6 space-y-4">
              {error && <div className="p-3 bg-red-950/40 border border-red-800/60 rounded-xl text-red-200 text-xs">{error}</div>}
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Full Name</label>
                <input
                  type="text"
                  required
                  value={formFullName}
                  onChange={(e) => setFormFullName(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Email Address</label>
                <input
                  type="email"
                  required
                  value={formEmail}
                  onChange={(e) => setFormEmail(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Role</label>
                <select
                  value={formRole}
                  onChange={(e: any) => setFormRole(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                >
                  <option value="Student">Student</option>
                  <option value="Teacher">Teacher</option>
                  <option value="Admin">Admin</option>
                </select>
              </div>
              {formRole === 'Student' && (
                <div>
                  <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Assigned Class</label>
                  <select
                    value={formClassId}
                    required
                    onChange={(e) => setFormClassId(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                  >
                    <option value="">Select a Class</option>
                    {classes.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name} ({c.grade}-{c.section})
                      </option>
                    ))}
                  </select>
                </div>
              )}
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">
                  Password {editingUser && <span className="text-[10px] text-slate-500 font-normal lowercase">(leave blank to keep current)</span>}
                </label>
                <input
                  type="password"
                  required={!editingUser}
                  value={formPassword}
                  onChange={(e) => setFormPassword(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                  placeholder="••••••••"
                />
              </div>

              <div className="flex gap-3 justify-end pt-4 border-t border-slate-800/80 mt-6">
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  className="px-4 py-2 border border-slate-800 text-slate-400 hover:text-white rounded-xl text-sm font-semibold hover:bg-slate-800/40 transition-all"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-sm font-semibold transition-all shadow-lg shadow-indigo-500/10"
                >
                  Save User
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

// ── CLASSES PANEL ───────────────────────────────────────────────────────────
function ClassesPanel() {
  const [classes, setClasses] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const [showModal, setShowModal] = useState(false);
  const [editingClass, setEditingClass] = useState<any | null>(null);
  const [formName, setFormName] = useState('');
  const [formGrade, setFormGrade] = useState('');
  const [formSection, setFormSection] = useState('');
  const [error, setError] = useState<string | null>(null);

  const fetchClasses = useCallback(async () => {
    setLoading(true);
    try {
      const res: any = await api.get(`/api/v1/classes?page=${page}&pageSize=10&search=${encodeURIComponent(search)}`);
      setClasses(res.data || []);
      setTotalPages(res.pagination?.totalPages || 1);
    } catch (err: any) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, [page, search]);

  useEffect(() => {
    fetchClasses();
  }, [fetchClasses]);

  const openCreateModal = () => {
    setEditingClass(null);
    setFormName('');
    setFormGrade('');
    setFormSection('');
    setError(null);
    setShowModal(true);
  };

  const openEditModal = (c: any) => {
    setEditingClass(c);
    setFormName(c.name);
    setFormGrade(c.grade || '');
    setFormSection(c.section || '');
    setError(null);
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      const payload = { name: formName, grade: formGrade || null, section: formSection || null };
      if (editingClass) {
        await api.put(`/api/v1/classes/${editingClass.id}`, payload);
      } else {
        await api.post('/api/v1/classes', payload);
      }
      setShowModal(false);
      fetchClasses();
    } catch (err: any) {
      setError(err.message || 'Failed to save class.');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this class? This may affect student references.')) return;
    try {
      await api.delete(`/api/v1/classes/${id}`);
      fetchClasses();
    } catch (err: any) {
      alert(err.message || 'Failed to delete class.');
    }
  };

  return (
    <div className="bg-slate-900/40 border border-slate-800 rounded-2xl p-6 flex flex-col gap-6 backdrop-blur-xl animate-fade-in">
      <div className="flex justify-between items-center flex-wrap gap-4">
        <div className="relative w-full max-w-xs">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-500" />
          <input
            type="text"
            placeholder="Search classes..."
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            className="w-full bg-slate-950/60 border border-slate-800 rounded-xl pl-10 pr-4 py-2 text-sm text-slate-200 outline-none focus:border-indigo-500"
          />
        </div>

        <button
          onClick={openCreateModal}
          className="flex items-center gap-2 bg-gradient-to-r from-indigo-500 to-indigo-400 hover:from-indigo-400 hover:to-indigo-300 text-slate-950 px-4 py-2 rounded-xl text-sm font-bold shadow-lg"
        >
          <FolderPlus className="h-4 w-4" />
          Create Class
        </button>
      </div>

      <div className="overflow-x-auto border border-slate-800/60 rounded-xl">
        <table className="w-full text-left text-sm text-slate-300">
          <thead className="bg-slate-950/40 text-slate-400 uppercase tracking-wider text-xs font-semibold">
            <tr>
              <th className="px-6 py-4">Class Name</th>
              <th className="px-6 py-4">Grade</th>
              <th className="px-6 py-4">Section</th>
              <th className="px-6 py-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {loading ? (
              <tr>
                <td colSpan={4} className="text-center py-8">
                  <RefreshCw className="h-5 w-5 animate-spin mx-auto text-indigo-500" />
                </td>
              </tr>
            ) : classes.length === 0 ? (
              <tr>
                <td colSpan={4} className="text-center py-8 text-slate-500">No classes found.</td>
              </tr>
            ) : (
              classes.map((c) => (
                <tr key={c.id} className="hover:bg-slate-800/20 transition-colors">
                  <td className="px-6 py-4 font-semibold text-white">{c.name}</td>
                  <td className="px-6 py-4 text-slate-400">{c.grade || '-'}</td>
                  <td className="px-6 py-4 text-slate-400">{c.section || '-'}</td>
                  <td className="px-6 py-4 text-right">
                    <div className="flex gap-2 justify-end">
                      <button onClick={() => openEditModal(c)} className="p-1.5 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-slate-200">
                        <Edit className="h-4 w-4" />
                      </button>
                      <button onClick={() => handleDelete(c.id)} className="p-1.5 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-red-400">
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="flex justify-between items-center text-sm text-slate-400">
        <div>Page {page} of {totalPages}</div>
        <div className="flex gap-2">
          <button disabled={page <= 1} onClick={() => setPage(page - 1)} className="p-2 border border-slate-800 rounded-lg hover:bg-slate-800/40 disabled:opacity-40 transition-all">
            <ChevronLeft className="h-4 w-4" />
          </button>
          <button disabled={page >= totalPages} onClick={() => setPage(page + 1)} className="p-2 border border-slate-800 rounded-lg hover:bg-slate-800/40 disabled:opacity-40 transition-all">
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>
      </div>

      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="w-full max-w-md bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden shadow-2xl relative">
            <div className="absolute top-0 left-0 right-0 h-[2px] bg-indigo-500"></div>
            <div className="p-6 flex items-center justify-between border-b border-slate-800">
              <h3 className="font-bold text-white text-lg">{editingClass ? 'Edit Class' : 'Create Class'}</h3>
              <button onClick={() => setShowModal(false)} className="text-slate-400 hover:text-white"><X className="h-5 w-5" /></button>
            </div>

            <form onSubmit={handleSubmit} className="p-6 space-y-4">
              {error && <div className="p-3 bg-red-950/40 border border-red-800/60 rounded-xl text-red-200 text-xs">{error}</div>}
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Class Name</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Science-A"
                  value={formName}
                  onChange={(e) => setFormName(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Grade (optional)</label>
                <input
                  type="text"
                  placeholder="e.g. 10"
                  value={formGrade}
                  onChange={(e) => setFormGrade(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Section (optional)</label>
                <input
                  type="text"
                  placeholder="e.g. A"
                  value={formSection}
                  onChange={(e) => setFormSection(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                />
              </div>

              <div className="flex gap-3 justify-end pt-4 border-t border-slate-800/80 mt-6">
                <button type="button" onClick={() => setShowModal(false)} className="px-4 py-2 border border-slate-800 text-slate-400 hover:text-white rounded-xl text-sm hover:bg-slate-800/40">Cancel</button>
                <button type="submit" className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-sm font-semibold shadow-lg">Save Class</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

// ── SUBJECTS PANEL ──────────────────────────────────────────────────────────
function SubjectsPanel() {
  const [subjects, setSubjects] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const [showModal, setShowModal] = useState(false);
  const [editingSubject, setEditingSubject] = useState<any | null>(null);
  const [formName, setFormName] = useState('');
  const [formCode, setFormCode] = useState('');
  const [error, setError] = useState<string | null>(null);

  const fetchSubjects = useCallback(async () => {
    setLoading(true);
    try {
      const res: any = await api.get(`/api/v1/subjects?page=${page}&pageSize=10&search=${encodeURIComponent(search)}`);
      setSubjects(res.data || []);
      setTotalPages(res.pagination?.totalPages || 1);
    } catch (err: any) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, [page, search]);

  useEffect(() => {
    fetchSubjects();
  }, [fetchSubjects]);

  const openCreateModal = () => {
    setEditingSubject(null);
    setFormName('');
    setFormCode('');
    setError(null);
    setShowModal(true);
  };

  const openEditModal = (s: any) => {
    setEditingSubject(s);
    setFormName(s.name);
    setFormCode(s.code);
    setError(null);
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      const payload = { name: formName, code: formCode };
      if (editingSubject) {
        await api.put(`/api/v1/subjects/${editingSubject.id}`, payload);
      } else {
        await api.post('/api/v1/subjects', payload);
      }
      setShowModal(false);
      fetchSubjects();
    } catch (err: any) {
      setError(err.message || 'Failed to save subject.');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this subject?')) return;
    try {
      await api.delete(`/api/v1/subjects/${id}`);
      fetchSubjects();
    } catch (err: any) {
      alert(err.message || 'Failed to delete subject.');
    }
  };

  return (
    <div className="bg-slate-900/40 border border-slate-800 rounded-2xl p-6 flex flex-col gap-6 backdrop-blur-xl animate-fade-in">
      <div className="flex justify-between items-center flex-wrap gap-4">
        <div className="relative w-full max-w-xs">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-500" />
          <input
            type="text"
            placeholder="Search subjects..."
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            className="w-full bg-slate-950/60 border border-slate-800 rounded-xl pl-10 pr-4 py-2 text-sm text-slate-200 outline-none focus:border-indigo-500"
          />
        </div>

        <button
          onClick={openCreateModal}
          className="flex items-center gap-2 bg-gradient-to-r from-indigo-500 to-indigo-400 hover:from-indigo-400 hover:to-indigo-300 text-slate-950 px-4 py-2 rounded-xl text-sm font-bold shadow-lg"
        >
          <BookPlus className="h-4 w-4" />
          Create Subject
        </button>
      </div>

      <div className="overflow-x-auto border border-slate-800/60 rounded-xl">
        <table className="w-full text-left text-sm text-slate-300">
          <thead className="bg-slate-950/40 text-slate-400 uppercase tracking-wider text-xs font-semibold">
            <tr>
              <th className="px-6 py-4">Subject Name</th>
              <th className="px-6 py-4">Code</th>
              <th className="px-6 py-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {loading ? (
              <tr>
                <td colSpan={3} className="text-center py-8">
                  <RefreshCw className="h-5 w-5 animate-spin mx-auto text-indigo-500" />
                </td>
              </tr>
            ) : subjects.length === 0 ? (
              <tr>
                <td colSpan={3} className="text-center py-8 text-slate-500">No subjects found.</td>
              </tr>
            ) : (
              subjects.map((s) => (
                <tr key={s.id} className="hover:bg-slate-800/20 transition-colors">
                  <td className="px-6 py-4 font-semibold text-white">{s.name}</td>
                  <td className="px-6 py-4"><span className="bg-slate-950 px-2 py-1 rounded text-xs font-mono text-slate-400 border border-slate-800/40">{s.code}</span></td>
                  <td className="px-6 py-4 text-right">
                    <div className="flex gap-2 justify-end">
                      <button onClick={() => openEditModal(s)} className="p-1.5 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-slate-200">
                        <Edit className="h-4 w-4" />
                      </button>
                      <button onClick={() => handleDelete(s.id)} className="p-1.5 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-red-400">
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="flex justify-between items-center text-sm text-slate-400">
        <div>Page {page} of {totalPages}</div>
        <div className="flex gap-2">
          <button disabled={page <= 1} onClick={() => setPage(page - 1)} className="p-2 border border-slate-800 rounded-lg hover:bg-slate-800/40 disabled:opacity-40 transition-all">
            <ChevronLeft className="h-4 w-4" />
          </button>
          <button disabled={page >= totalPages} onClick={() => setPage(page + 1)} className="p-2 border border-slate-800 rounded-lg hover:bg-slate-800/40 disabled:opacity-40 transition-all">
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>
      </div>

      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="w-full max-w-md bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden shadow-2xl relative">
            <div className="absolute top-0 left-0 right-0 h-[2px] bg-indigo-500"></div>
            <div className="p-6 flex items-center justify-between border-b border-slate-800">
              <h3 className="font-bold text-white text-lg">{editingSubject ? 'Edit Subject' : 'Create Subject'}</h3>
              <button onClick={() => setShowModal(false)} className="text-slate-400 hover:text-white"><X className="h-5 w-5" /></button>
            </div>

            <form onSubmit={handleSubmit} className="p-6 space-y-4">
              {error && <div className="p-3 bg-red-950/40 border border-red-800/60 rounded-xl text-red-200 text-xs">{error}</div>}
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Subject Name</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Mathematics"
                  value={formName}
                  onChange={(e) => setFormName(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Subject Code</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. MATH-101"
                  value={formCode}
                  onChange={(e) => setFormCode(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                />
              </div>

              <div className="flex gap-3 justify-end pt-4 border-t border-slate-800/80 mt-6">
                <button type="button" onClick={() => setShowModal(false)} className="px-4 py-2 border border-slate-800 text-slate-400 hover:text-white rounded-xl text-sm hover:bg-slate-800/40">Cancel</button>
                <button type="submit" className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-sm font-semibold shadow-lg">Save Subject</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

// ── TEACHER MAPPINGS PANEL ──────────────────────────────────────────────────
function MappingsPanel() {
  const [mappings, setMappings] = useState<any[]>([]);
  const [teachers, setTeachers] = useState<any[]>([]);
  const [subjects, setSubjects] = useState<any[]>([]);
  const [classes, setClasses] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const [showModal, setShowModal] = useState(false);
  const [formTeacherId, setFormTeacherId] = useState('');
  const [formSubjectId, setFormSubjectId] = useState('');
  const [formClassId, setFormClassId] = useState('');
  const [error, setError] = useState<string | null>(null);

  const fetchMappings = useCallback(async () => {
    setLoading(true);
    try {
      const res: any = await api.get(`/api/v1/teacher-assignments?page=${page}&pageSize=10`);
      setMappings(res.data || []);
      setTotalPages(res.pagination?.totalPages || 1);
    } catch (err: any) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, [page]);

  const loadDropdowns = async () => {
    try {
      const [resUsers, resSubjects, resClasses]: any = await Promise.all([
        api.get('/api/v1/users?role=Teacher&pageSize=100'),
        api.get('/api/v1/subjects?pageSize=100'),
        api.get('/api/v1/classes?pageSize=100'),
      ]);
      setTeachers(resUsers.data || []);
      setSubjects(resSubjects.data || []);
      setClasses(resClasses.data || []);
    } catch (err) {
      console.error(err);
    }
  };

  useEffect(() => {
    fetchMappings();
  }, [fetchMappings]);

  useEffect(() => {
    loadDropdowns();
  }, []);

  const openCreateModal = () => {
    setFormTeacherId('');
    setFormSubjectId('');
    setFormClassId('');
    setError(null);
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      await api.post('/api/v1/teacher-assignments', {
        teacherId: formTeacherId,
        subjectId: formSubjectId,
        classId: formClassId
      });
      setShowModal(false);
      fetchMappings();
    } catch (err: any) {
      setError(err.message || 'Failed to map teacher.');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this mapping?')) return;
    try {
      await api.delete(`/api/v1/teacher-assignments/${id}`);
      fetchMappings();
    } catch (err: any) {
      alert(err.message || 'Failed to delete mapping.');
    }
  };

  return (
    <div className="bg-slate-900/40 border border-slate-800 rounded-2xl p-6 flex flex-col gap-6 backdrop-blur-xl animate-fade-in">
      <div className="flex justify-between items-center">
        <h2 className="text-lg font-bold text-white">Teacher Assignments Mapping</h2>
        <button
          onClick={openCreateModal}
          className="flex items-center gap-2 bg-gradient-to-r from-indigo-500 to-indigo-400 hover:from-indigo-400 hover:to-indigo-300 text-slate-950 px-4 py-2 rounded-xl text-sm font-bold shadow-lg"
        >
          <Plus className="h-4 w-4" />
          Assign Teacher
        </button>
      </div>

      <div className="overflow-x-auto border border-slate-800/60 rounded-xl">
        <table className="w-full text-left text-sm text-slate-300">
          <thead className="bg-slate-950/40 text-slate-400 uppercase tracking-wider text-xs font-semibold">
            <tr>
              <th className="px-6 py-4">Teacher</th>
              <th className="px-6 py-4">Subject</th>
              <th className="px-6 py-4">Class</th>
              <th className="px-6 py-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {loading ? (
              <tr>
                <td colSpan={4} className="text-center py-8">
                  <RefreshCw className="h-5 w-5 animate-spin mx-auto text-indigo-500" />
                </td>
              </tr>
            ) : mappings.length === 0 ? (
              <tr>
                <td colSpan={4} className="text-center py-8 text-slate-500">No teacher mappings created yet.</td>
              </tr>
            ) : (
              mappings.map((m) => (
                <tr key={m.id} className="hover:bg-slate-800/20 transition-colors">
                  <td className="px-6 py-4">
                    <div>
                      <p className="font-semibold text-white">{m.teacherName}</p>
                      <p className="text-xs text-slate-500">{m.teacherEmail}</p>
                    </div>
                  </td>
                  <td className="px-6 py-4 text-slate-400">{m.subjectName} ({m.subjectCode})</td>
                  <td className="px-6 py-4 text-slate-400">{m.className}</td>
                  <td className="px-6 py-4 text-right">
                    <button onClick={() => handleDelete(m.id)} className="p-1.5 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-red-400">
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="flex justify-between items-center text-sm text-slate-400">
        <div>Page {page} of {totalPages}</div>
        <div className="flex gap-2">
          <button disabled={page <= 1} onClick={() => setPage(page - 1)} className="p-2 border border-slate-800 rounded-lg hover:bg-slate-800/40 disabled:opacity-40 transition-all">
            <ChevronLeft className="h-4 w-4" />
          </button>
          <button disabled={page >= totalPages} onClick={() => setPage(page + 1)} className="p-2 border border-slate-800 rounded-lg hover:bg-slate-800/40 disabled:opacity-40 transition-all">
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>
      </div>

      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="w-full max-w-md bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden shadow-2xl relative">
            <div className="absolute top-0 left-0 right-0 h-[2px] bg-indigo-500"></div>
            <div className="p-6 flex items-center justify-between border-b border-slate-800">
              <h3 className="font-bold text-white text-lg">Assign Teacher</h3>
              <button onClick={() => setShowModal(false)} className="text-slate-400 hover:text-white"><X className="h-5 w-5" /></button>
            </div>

            <form onSubmit={handleSubmit} className="p-6 space-y-4">
              {error && <div className="p-3 bg-red-950/40 border border-red-800/60 rounded-xl text-red-200 text-xs">{error}</div>}
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Select Teacher</label>
                <select
                  value={formTeacherId}
                  required
                  onChange={(e) => setFormTeacherId(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                >
                  <option value="">Choose a Teacher</option>
                  {teachers.map((t) => (
                    <option key={t.id} value={t.id}>{t.fullName} ({t.email})</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Select Subject</label>
                <select
                  value={formSubjectId}
                  required
                  onChange={(e) => setFormSubjectId(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                >
                  <option value="">Choose a Subject</option>
                  {subjects.map((s) => (
                    <option key={s.id} value={s.id}>{s.name} ({s.code})</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Select Class</label>
                <select
                  value={formClassId}
                  required
                  onChange={(e) => setFormClassId(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                >
                  <option value="">Choose a Class</option>
                  {classes.map((c) => (
                    <option key={c.id} value={c.id}>{c.name} ({c.grade}-{c.section})</option>
                  ))}
                </select>
              </div>

              <div className="flex gap-3 justify-end pt-4 border-t border-slate-800/80 mt-6">
                <button type="button" onClick={() => setShowModal(false)} className="px-4 py-2 border border-slate-800 text-slate-400 hover:text-white rounded-xl text-sm hover:bg-slate-800/40">Cancel</button>
                <button type="submit" className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-sm font-semibold shadow-lg">Map Assignment</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
