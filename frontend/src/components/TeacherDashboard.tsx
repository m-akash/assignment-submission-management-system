'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { api } from '@/lib/api';
import { 
  FileText, Plus, CheckCircle, Clock, AlertCircle, Award, 
  Download, Eye, Search, Trash2, Edit, X, RefreshCw, ClipboardCheck,
  ChevronLeft, ChevronRight
} from 'lucide-react';

export default function TeacherDashboard() {
  const [activeTab, setActiveTab] = useState<'assignments' | 'submissions'>('assignments');

  return (
    <div className="flex-1 flex flex-col gap-6">
      {/* Tab Selector */}
      <div className="flex border-b border-slate-800 bg-slate-900/40 p-1.5 rounded-xl self-start">
        <button
          onClick={() => setActiveTab('assignments')}
          className={`flex items-center gap-2 px-4 py-2 text-sm font-semibold rounded-lg transition-all duration-200 ${
            activeTab === 'assignments'
              ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-500/10'
              : 'text-slate-400 hover:text-slate-200'
          }`}
        >
          <FileText className="h-4 w-4" />
          Assignments
        </button>
        <button
          onClick={() => setActiveTab('submissions')}
          className={`flex items-center gap-2 px-4 py-2 text-sm font-semibold rounded-lg transition-all duration-200 ${
            activeTab === 'submissions'
              ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-500/10'
              : 'text-slate-400 hover:text-slate-200'
          }`}
        >
          <ClipboardCheck className="h-4 w-4" />
          Submissions
        </button>
      </div>

      {/* Tab Panels */}
      <div className="flex-1 flex flex-col">
        {activeTab === 'assignments' && <AssignmentsPanel />}
        {activeTab === 'submissions' && <SubmissionsPanel />}
      </div>
    </div>
  );
}

// ── ASSIGNMENTS PANEL ────────────────────────────────────────────────────────
function AssignmentsPanel() {
  const [assignments, setAssignments] = useState<any[]>([]);
  const [mappings, setMappings] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  // Form states
  const [showModal, setShowModal] = useState(false);
  const [editingAssignment, setEditingAssignment] = useState<any | null>(null);
  const [formMappingId, setFormMappingId] = useState('');
  const [formTitle, setFormTitle] = useState('');
  const [formDescription, setFormDescription] = useState('');
  const [formDeadline, setFormDeadline] = useState('');
  const [formMaxMarks, setFormMaxMarks] = useState('100');
  const [formAllowResubmission, setFormAllowResubmission] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchAssignments = useCallback(async () => {
    setLoading(true);
    try {
      const res: any = await api.get(`/api/v1/assignments?page=${page}&pageSize=10`);
      setAssignments(res.data || []);
      setTotalPages(res.pagination?.totalPages || 1);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, [page]);

  const fetchMappings = async () => {
    try {
      const res: any = await api.get('/api/v1/teacher-assignments?pageSize=100');
      setMappings(res.data || []);
    } catch (err) {
      console.error(err);
    }
  };

  useEffect(() => {
    fetchAssignments();
  }, [fetchAssignments]);

  useEffect(() => {
    fetchMappings();
  }, []);

  const openCreateModal = () => {
    setEditingAssignment(null);
    setFormMappingId(mappings[0]?.id || '');
    setFormTitle('');
    setFormDescription('');
    // Default deadline: tomorrow at current time
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    // Format to yyyy-MM-ddThh:mm
    const year = tomorrow.getFullYear();
    const month = String(tomorrow.getMonth() + 1).padStart(2, '0');
    const day = String(tomorrow.getDate()).padStart(2, '0');
    const hours = String(tomorrow.getHours()).padStart(2, '0');
    const minutes = String(tomorrow.getMinutes()).padStart(2, '0');
    setFormDeadline(`${year}-${month}-${day}T${hours}:${minutes}`);
    setFormMaxMarks('100');
    setFormAllowResubmission(true);
    setError(null);
    setShowModal(true);
  };

  const openEditModal = (a: any) => {
    setEditingAssignment(a);
    setFormMappingId(a.teacherAssignmentId || '');
    setFormTitle(a.title);
    setFormDescription(a.description);
    // Convert UTC deadline to local ISO string for datetime-local input
    const d = new Date(a.deadlineUtc);
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    const hours = String(d.getHours()).padStart(2, '0');
    const minutes = String(d.getMinutes()).padStart(2, '0');
    setFormDeadline(`${year}-${month}-${day}T${hours}:${minutes}`);
    setFormMaxMarks(String(a.maxMarks));
    setFormAllowResubmission(a.allowResubmission);
    setError(null);
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    // Convert local deadline input string to UTC Date object
    const dateLocal = new Date(formDeadline);
    const deadlineUtc = dateLocal.toISOString();

    const payload = {
      teacherAssignmentId: formMappingId,
      title: formTitle,
      description: formDescription,
      deadlineUtc,
      maxMarks: parseFloat(formMaxMarks),
      allowResubmission: formAllowResubmission
    };

    try {
      if (editingAssignment) {
        await api.put(`/api/v1/assignments/${editingAssignment.id}`, {
          title: formTitle,
          description: formDescription,
          deadlineUtc,
          maxMarks: parseFloat(formMaxMarks),
          allowResubmission: formAllowResubmission
        });
      } else {
        await api.post('/api/v1/assignments', payload);
      }
      setShowModal(false);
      fetchAssignments();
    } catch (err: any) {
      setError(err.message || 'Failed to save assignment.');
    }
  };

  const handlePublish = async (id: string) => {
    if (!confirm('Are you sure you want to publish this assignment? Once published, students will see it immediately.')) return;
    try {
      await api.post(`/api/v1/assignments/${id}/publish`);
      fetchAssignments();
    } catch (err: any) {
      alert(err.message || 'Failed to publish assignment.');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this assignment?')) return;
    try {
      await api.delete(`/api/v1/assignments/${id}`);
      fetchAssignments();
    } catch (err: any) {
      alert(err.message || 'Failed to delete assignment.');
    }
  };

  return (
    <div className="bg-slate-900/40 border border-slate-800 rounded-2xl p-6 flex flex-col gap-6 backdrop-blur-xl animate-fade-in">
      <div className="flex justify-between items-center">
        <h2 className="text-lg font-bold text-white">Assignments Management</h2>
        <button
          onClick={openCreateModal}
          className="flex items-center gap-2 bg-gradient-to-r from-indigo-500 to-indigo-400 hover:from-indigo-400 hover:to-indigo-300 text-slate-950 px-4 py-2 rounded-xl text-sm font-bold shadow-lg"
        >
          <Plus className="h-4 w-4" />
          Create Assignment
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {loading ? (
          <div className="col-span-2 text-center py-8">
            <RefreshCw className="h-5 w-5 animate-spin mx-auto text-indigo-500" />
          </div>
        ) : assignments.length === 0 ? (
          <div className="col-span-2 text-center py-8 text-slate-500">No assignments created yet.</div>
        ) : (
          assignments.map((a) => (
            <div key={a.id} className="bg-slate-950/40 border border-slate-800 rounded-2xl p-5 flex flex-col gap-4 relative overflow-hidden">
              <div className="flex justify-between items-start">
                <div>
                  <span className={`inline-flex items-center px-2 py-0.5 rounded text-[10px] font-semibold border ${
                    a.status === 'Published'
                      ? 'bg-emerald-950/40 border-emerald-800 text-emerald-300'
                      : 'bg-amber-950/40 border-amber-800 text-amber-300'
                  }`}>
                    {a.status}
                  </span>
                  <h3 className="text-base font-bold text-white mt-2">{a.title}</h3>
                  <p className="text-xs text-slate-400 mt-1">{a.className} • {a.subjectName}</p>
                </div>
                <div className="flex gap-1.5">
                  {a.status === 'Draft' && (
                    <button
                      onClick={() => handlePublish(a.id)}
                      className="px-2.5 py-1 bg-emerald-600 hover:bg-emerald-500 text-slate-950 rounded-lg text-xs font-bold transition-all"
                    >
                      Publish
                    </button>
                  )}
                  <button onClick={() => openEditModal(a)} className="p-1.5 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-slate-200">
                    <Edit className="h-3.5 w-3.5" />
                  </button>
                  <button onClick={() => handleDelete(a.id)} className="p-1.5 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-red-400">
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </div>
              </div>

              <p className="text-xs text-slate-300 line-clamp-2">{a.description}</p>

              <div className="grid grid-cols-2 gap-3 pt-3 border-t border-slate-900 text-xs text-slate-400">
                <div className="flex items-center gap-1.5">
                  <Clock className="h-3.5 w-3.5 text-indigo-400" />
                  <span>Due: {new Date(a.deadlineUtc).toLocaleDateString()}</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <Award className="h-3.5 w-3.5 text-emerald-400" />
                  <span>Max Marks: {a.maxMarks}</span>
                </div>
              </div>
            </div>
          ))
        )}
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
              <h3 className="font-bold text-white text-lg">{editingAssignment ? 'Edit Assignment' : 'Create Assignment'}</h3>
              <button onClick={() => setShowModal(false)} className="text-slate-400 hover:text-white"><X className="h-5 w-5" /></button>
            </div>

            <form onSubmit={handleSubmit} className="p-6 space-y-4">
              {error && <div className="p-3 bg-red-950/40 border border-red-800/60 rounded-xl text-red-200 text-xs">{error}</div>}
              
              {!editingAssignment && (
                <div>
                  <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Class & Subject (Mapping)</label>
                  <select
                    value={formMappingId}
                    required
                    onChange={(e) => setFormMappingId(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                  >
                    <option value="">Choose Class & Subject Mapping</option>
                    {mappings.map((m) => (
                      <option key={m.id} value={m.id}>
                        {m.className} - {m.subjectName} ({m.subjectCode})
                      </option>
                    ))}
                  </select>
                </div>
              )}

              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Title</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Homework 1"
                  value={formTitle}
                  onChange={(e) => setFormTitle(e.target.value)}
                  disabled={editingAssignment && editingAssignment.status === 'Published' && editingAssignment.submissionCount > 0}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none disabled:opacity-50"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Instructions / Description</label>
                <textarea
                  required
                  rows={4}
                  placeholder="Write the questions or submission instructions..."
                  value={formDescription}
                  onChange={(e) => setFormDescription(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none resize-none"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Max Marks</label>
                  <input
                    type="number"
                    required
                    min="1"
                    value={formMaxMarks}
                    onChange={(e) => setFormMaxMarks(e.target.value)}
                    disabled={editingAssignment && editingAssignment.status === 'Published' && editingAssignment.submissionCount > 0}
                    className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none disabled:opacity-50"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Deadline (Local Time)</label>
                  <input
                    type="datetime-local"
                    required
                    value={formDeadline}
                    onChange={(e) => setFormDeadline(e.target.value)}
                    disabled={editingAssignment && editingAssignment.status === 'Published' && editingAssignment.submissionCount > 0}
                    className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none disabled:opacity-50"
                  />
                </div>
              </div>

              <div className="flex items-center gap-3 pt-2">
                <input
                  type="checkbox"
                  id="allowResubmission"
                  checked={formAllowResubmission}
                  onChange={(e) => setFormAllowResubmission(e.target.checked)}
                  disabled={editingAssignment && editingAssignment.status === 'Published' && editingAssignment.submissionCount > 0}
                  className="h-4 w-4 rounded bg-slate-950 border-slate-800 text-indigo-600 focus:ring-indigo-500 disabled:opacity-50"
                />
                <label htmlFor="allowResubmission" className="text-sm font-medium text-slate-300">
                  Allow Resubmission
                </label>
              </div>

              <div className="flex gap-3 justify-end pt-4 border-t border-slate-800/80 mt-6">
                <button type="button" onClick={() => setShowModal(false)} className="px-4 py-2 border border-slate-800 text-slate-400 hover:text-white rounded-xl text-sm hover:bg-slate-800/40">Cancel</button>
                <button type="submit" className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-sm font-semibold shadow-lg">Save Assignment</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

// ── SUBMISSIONS PANEL ───────────────────────────────────────────────────────
function SubmissionsPanel() {
  const [submissions, setSubmissions] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  // Grading Modal Form states
  const [showGradeModal, setShowGradeModal] = useState(false);
  const [selectedSubmission, setSelectedSubmission] = useState<any | null>(null);
  const [formMarks, setFormMarks] = useState('');
  const [formFeedback, setFormFeedback] = useState('');
  const [error, setError] = useState<string | null>(null);

  const fetchSubmissions = useCallback(async () => {
    setLoading(true);
    try {
      const res: any = await api.get(`/api/v1/submissions?page=${page}&pageSize=10`);
      setSubmissions(res.data || []);
      setTotalPages(res.pagination?.totalPages || 1);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => {
    fetchSubmissions();
  }, [fetchSubmissions]);

  const openGradeModal = (sub: any) => {
    setSelectedSubmission(sub);
    setFormMarks(sub.marks !== null ? String(sub.marks) : '');
    setFormFeedback(sub.feedback || '');
    setError(null);
    setShowGradeModal(true);
  };

  const handleGradeSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const marksNum = parseFloat(formMarks);
    if (isNaN(marksNum) || marksNum < 0) {
      setError('Please enter a valid non-negative mark.');
      return;
    }

    try {
      await api.post(`/api/v1/submissions/${selectedSubmission.id}/review`, {
        marks: marksNum,
        feedback: formFeedback,
        status: 'Graded'
      });
      setShowGradeModal(false);
      fetchSubmissions();
    } catch (err: any) {
      setError(err.message || 'Failed to submit grade.');
    }
  };

  const handleDownload = async (fileId: string, fileName: string) => {
    try {
      const response = await api.get(`/api/v1/submissions/files/${fileId}`, {
        responseType: 'blob'
      });
      
      // Axios response type blob won't trigger standard unpacked response interceptor because it's a blob.
      // Wait, let's make sure the response object itself is accessed correctly.
      const blob = response instanceof Blob ? response : (response as any).data || response;
      const url = window.URL.createObjectURL(new Blob([blob]));
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', fileName);
      document.body.appendChild(link);
      link.click();
      link.remove();
    } catch (err: any) {
      alert('Failed to download file.');
    }
  };

  return (
    <div className="bg-slate-900/40 border border-slate-800 rounded-2xl p-6 flex flex-col gap-6 backdrop-blur-xl animate-fade-in">
      <h2 className="text-lg font-bold text-white">Student Submissions</h2>

      <div className="overflow-x-auto border border-slate-800/60 rounded-xl">
        <table className="w-full text-left text-sm text-slate-300">
          <thead className="bg-slate-950/40 text-slate-400 uppercase tracking-wider text-xs font-semibold">
            <tr>
              <th className="px-6 py-4">Student</th>
              <th className="px-6 py-4">Assignment</th>
              <th className="px-6 py-4">Submitted At</th>
              <th className="px-6 py-4">Status</th>
              <th className="px-6 py-4">Grade</th>
              <th className="px-6 py-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {loading ? (
              <tr>
                <td colSpan={6} className="text-center py-8">
                  <RefreshCw className="h-5 w-5 animate-spin mx-auto text-indigo-500" />
                </td>
              </tr>
            ) : submissions.length === 0 ? (
              <tr>
                <td colSpan={6} className="text-center py-8 text-slate-500">No submissions received yet.</td>
              </tr>
            ) : (
              submissions.map((sub) => (
                <tr key={sub.id} className="hover:bg-slate-800/20 transition-colors">
                  <td className="px-6 py-4 font-semibold text-white">{sub.studentName}</td>
                  <td className="px-6 py-4 text-slate-300 font-medium">{sub.assignmentTitle}</td>
                  <td className="px-6 py-4 text-slate-400">{new Date(sub.submittedAtUtc).toLocaleString()}</td>
                  <td className="px-6 py-4">
                    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${
                      sub.status === 'Graded'
                        ? 'bg-emerald-950/40 border-emerald-800 text-emerald-300'
                        : sub.status === 'Submitted'
                        ? 'bg-blue-950/40 border-blue-800 text-blue-300'
                        : 'bg-red-950/40 border-red-800 text-red-300'
                    }`}>
                      {sub.status}
                    </span>
                  </td>
                  <td className="px-6 py-4 font-semibold text-white">
                    {sub.marks !== null ? `${sub.marks} / ${sub.assignmentMaxMarks || 100}` : '-'}
                  </td>
                  <td className="px-6 py-4 text-right">
                    <div className="flex gap-2 justify-end items-center">
                      {sub.files && sub.files.map((file: any) => (
                        <button
                          key={file.id}
                          onClick={() => handleDownload(file.id, file.originalFileName || file.fileName)}
                          title={`Download ${file.originalFileName || file.fileName}`}
                          className="p-1.5 bg-slate-950 hover:bg-slate-800 border border-slate-800 rounded-lg text-slate-400 hover:text-white transition-all flex items-center gap-1 text-xs"
                        >
                          <Download className="h-3.5 w-3.5" />
                          File
                        </button>
                      ))}
                      <button
                        onClick={() => openGradeModal(sub)}
                        className="flex items-center gap-1.5 bg-indigo-600/90 hover:bg-indigo-600 text-white px-3 py-1.5 rounded-lg text-xs font-semibold transition-all"
                      >
                        <Award className="h-3.5 w-3.5" />
                        Grade
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

      {showGradeModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="w-full max-w-md bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden shadow-2xl relative">
            <div className="absolute top-0 left-0 right-0 h-[2px] bg-indigo-500"></div>
            <div className="p-6 flex items-center justify-between border-b border-slate-800">
              <h3 className="font-bold text-white text-lg">Grade Submission</h3>
              <button onClick={() => setShowGradeModal(false)} className="text-slate-400 hover:text-white"><X className="h-5 w-5" /></button>
            </div>

            <form onSubmit={handleGradeSubmit} className="p-6 space-y-4">
              {error && <div className="p-3 bg-red-950/40 border border-red-800/60 rounded-xl text-red-200 text-xs">{error}</div>}
              
              <div className="bg-slate-950/40 p-4 border border-slate-800/60 rounded-xl text-sm space-y-2">
                <p><span className="text-slate-500">Student:</span> <span className="font-semibold text-white">{selectedSubmission.studentName}</span></p>
                <p><span className="text-slate-500">Assignment:</span> <span className="font-semibold text-white">{selectedSubmission.assignmentTitle}</span></p>
                <p><span className="text-slate-500">Student's Answer:</span></p>
                <p className="bg-slate-950 p-3 rounded-lg border border-slate-800 font-mono text-xs whitespace-pre-wrap text-slate-300 max-h-40 overflow-y-auto">
                  {selectedSubmission.content || '[No text answer provided]'}
                </p>
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Marks Assigned</label>
                <input
                  type="number"
                  step="0.01"
                  required
                  value={formMarks}
                  onChange={(e) => setFormMarks(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none"
                  placeholder="e.g. 90"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Teacher Feedback (optional)</label>
                <textarea
                  rows={3}
                  value={formFeedback}
                  onChange={(e) => setFormFeedback(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none resize-none"
                  placeholder="Add feedback for the student..."
                />
              </div>

              <div className="flex gap-3 justify-end pt-4 border-t border-slate-800/80 mt-6">
                <button type="button" onClick={() => setShowGradeModal(false)} className="px-4 py-2 border border-slate-800 text-slate-400 hover:text-white rounded-xl text-sm hover:bg-slate-800/40">Cancel</button>
                <button type="submit" className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-sm font-semibold shadow-lg">Submit Grade</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
