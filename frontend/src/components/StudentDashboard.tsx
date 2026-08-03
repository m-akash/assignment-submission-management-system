'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { api } from '@/lib/api';
import { 
  FileText, Clock, Award, CheckCircle, AlertCircle, 
  Upload, X, RefreshCw, Send, Trash2, Download,
  ChevronLeft, ChevronRight
} from 'lucide-react';

export default function StudentDashboard() {
  const [assignments, setAssignments] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [filter, setFilter] = useState<'all' | 'pending' | 'submitted' | 'graded'>('all');

  // Submit flow states
  const [selectedAssignment, setSelectedAssignment] = useState<any | null>(null);
  const [submission, setSubmission] = useState<any | null>(null);
  const [submissionContent, setSubmissionContent] = useState('');
  const [attachedFiles, setAttachedFiles] = useState<any[]>([]);
  const [uploading, setUploading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchAssignments = useCallback(async () => {
    setLoading(true);
    try {
      const res: any = await api.get('/api/v1/assignments?pageSize=50');
      // Fetch submissions for each assignment to show status
      const assignmentsList = res.data || [];

      const updated = await Promise.all(
        assignmentsList.map(async (a: any) => {
          try {
            const subRes: any = await api.get(`/api/v1/assignments/${a.id}/submissions/me`);
            return { ...a, submission: subRes.data };
          } catch {
            return { ...a, submission: null };
          }
        })
      );
      setAssignments(updated);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchAssignments();
  }, [fetchAssignments]);

  const openSubmitModal = (assignment: any) => {
    setSelectedAssignment(assignment);
    const existingSub = assignment.submission;
    setSubmission(existingSub);
    setSubmissionContent(existingSub?.content || '');
    setAttachedFiles(existingSub?.files || []);
    setError(null);
  };

  // Mirrors FileStorage:AllowedExtensions on the server, which re-checks every upload
  // against the file's actual signature. This list is UX only.
  const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx', '.txt', '.png', '.jpg', '.jpeg'];

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (!files || files.length === 0) return;

    const file = files[0];
    const ext = '.' + file.name.split('.').pop()?.toLowerCase();
    if (!ALLOWED_EXTENSIONS.includes(ext)) {
      setError(`Invalid file type "${ext}". Allowed: ${ALLOWED_EXTENSIONS.join(', ')}.`);
      e.target.value = '';
      return;
    }

    setUploading(true);
    setError(null);

    const formData = new FormData();
    formData.append('file', file);

    try {
      // API call to upload file
      const res: any = await api.post(
        `/api/v1/assignments/${selectedAssignment.id}/submissions/upload`,
        formData,
        {
          headers: {
            'Content-Type': 'multipart/form-data',
          },
        }
      );
      // Add uploaded file DTO to UI list
      setAttachedFiles((prev) => [...prev, res.data]);
    } catch (err: any) {
      setError(err.message || 'File upload failed (limit: 10 MB, max 3 files per submission).');
    } finally {
      setUploading(false);
      // Reset input value
      e.target.value = '';
    }
  };

  const handleDeleteFile = async (fileId: string) => {
    try {
      await api.delete(`/api/v1/submissions/files/${fileId}`);
      setAttachedFiles((prev) => prev.filter((f) => f.id !== fileId));
    } catch (err: any) {
      setError(err.message || 'Failed to delete file.');
    }
  };

  const handleSubmissionSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!submissionContent && attachedFiles.length === 0) {
      setError('Please add a text answer or upload a file.');
      return;
    }

    setSubmitting(true);
    setError(null);

    // Attachments are already associated with the submission by the upload call, so the
    // body only carries the text answer.
    const payload = { content: submissionContent };

    try {
      if (submission) {
        // Update submission
        await api.put(`/api/v1/submissions/${submission.id}`, payload);
      } else {
        // Create new submission
        await api.post(`/api/v1/assignments/${selectedAssignment.id}/submissions`, payload);
      }
      setSelectedAssignment(null);
      fetchAssignments();
    } catch (err: any) {
      setError(err.message || 'Submission failed.');
    } finally {
      setSubmitting(false);
    }
  };

  const filteredAssignments = assignments.filter((a) => {
    if (filter === 'pending') return !a.submission || (a.submission.status !== 'Graded' && a.submission.status !== 'Submitted' && a.submission.status !== 'Late');
    if (filter === 'submitted') return a.submission && (a.submission.status === 'Submitted' || a.submission.status === 'Late');
    if (filter === 'graded') return a.submission && a.submission.status === 'Graded';
    return true;
  });

  const handleDownloadFile = async (fileId: string, fileName: string) => {
    try {
      const response = await api.get(`/api/v1/submissions/files/${fileId}`, {
        responseType: 'blob'
      });
      const blob = response instanceof Blob ? response : (response as any).data || response;
      const url = window.URL.createObjectURL(new Blob([blob]));
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', fileName);
      document.body.appendChild(link);
      link.click();
      link.remove();
    } catch (err) {
      alert('Failed to download file.');
    }
  };

  return (
    <div className="flex-1 flex flex-col gap-6 animate-fade-in">
      {/* Sub-header Filter */}
      <div className="flex justify-between items-center flex-wrap gap-4 bg-slate-900/40 p-4 border border-slate-800 rounded-2xl backdrop-blur-xl">
        <div className="flex gap-2">
          {(['all', 'pending', 'submitted', 'graded'] as const).map((tab) => (
            <button
              key={tab}
              onClick={() => setFilter(tab)}
              className={`px-4 py-1.5 rounded-lg text-xs font-semibold capitalize transition-all ${
                filter === tab
                  ? 'bg-indigo-600 text-white shadow-lg'
                  : 'text-slate-400 hover:text-slate-200'
              }`}
            >
              {tab}
            </button>
          ))}
        </div>
      </div>

      {/* Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
        {loading ? (
          <div className="col-span-2 text-center py-8">
            <RefreshCw className="h-5 w-5 animate-spin mx-auto text-indigo-500" />
          </div>
        ) : filteredAssignments.length === 0 ? (
          <div className="col-span-2 text-center py-8 text-slate-500">No assignments found in this section.</div>
        ) : (
          filteredAssignments.map((a) => {
            const hasSubmission = !!a.submission;
            const subStatus = a.submission?.status;

            return (
              <div key={a.id} className="bg-slate-900/40 border border-slate-800 rounded-2xl p-5 flex flex-col justify-between gap-4 backdrop-blur-xl relative overflow-hidden">
                <div>
                  <div className="flex justify-between items-start gap-2">
                    <span className="text-[10px] font-mono text-indigo-400 bg-indigo-950/40 border border-indigo-900/40 px-2 py-0.5 rounded">
                      {a.subjectName}
                    </span>
                    {hasSubmission ? (
                      <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-bold border ${
                        subStatus === 'Graded'
                          ? 'bg-emerald-950/40 border-emerald-800 text-emerald-300'
                          : subStatus === 'Submitted'
                          ? 'bg-blue-950/40 border-blue-800 text-blue-300'
                          : 'bg-amber-950/40 border-amber-800 text-amber-300'
                      }`}>
                        {subStatus === 'Graded' && <CheckCircle className="h-3 w-3" />}
                        {subStatus === 'Submitted' && <Clock className="h-3 w-3" />}
                        {subStatus}
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-bold border bg-slate-950/60 border-slate-800 text-slate-400">
                        Assigned
                      </span>
                    )}
                  </div>

                  <h3 className="text-base font-bold text-white mt-3">{a.title}</h3>
                  <p className="text-xs text-slate-400 mt-0.5">Teacher: {a.teacherName}</p>
                  <p className="text-xs text-slate-300 mt-3 line-clamp-3">{a.description}</p>
                </div>

                <div className="space-y-4">
                  {/* Submission outcome if graded */}
                  {subStatus === 'Graded' && (
                    <div className="bg-emerald-950/20 border border-emerald-900/30 p-3 rounded-xl text-xs text-emerald-300 space-y-1">
                      <p className="font-semibold flex justify-between">
                        <span>Grade:</span>
                        <span>{a.submission.marks} / {a.maxMarks} ({((a.submission.marks / a.maxMarks) * 100).toFixed(0)}%)</span>
                      </p>
                      {a.submission.feedback && (
                        <p className="text-emerald-400/90 mt-1"><span className="text-slate-400">Feedback:</span> "{a.submission.feedback}"</p>
                      )}
                    </div>
                  )}

                  <div className="flex justify-between items-center border-t border-slate-800/80 pt-4 text-xs text-slate-400">
                    <div className="flex items-center gap-1.5">
                      <Clock className="h-3.5 w-3.5 text-indigo-400" />
                      <span>Due: {new Date(a.deadlineUtc).toLocaleDateString()}</span>
                    </div>

                    <button
                      onClick={() => openSubmitModal(a)}
                      className={`px-4 py-2 rounded-xl text-xs font-bold transition-all shadow-md ${
                        hasSubmission
                          ? a.allowResubmission && new Date(a.deadlineUtc) > new Date()
                            ? 'bg-slate-800 hover:bg-slate-700 text-white'
                            : 'bg-slate-950 border border-slate-800 text-slate-500 cursor-not-allowed'
                          : 'bg-indigo-600 hover:bg-indigo-500 text-white'
                      }`}
                      disabled={hasSubmission && (!a.allowResubmission || new Date(a.deadlineUtc) <= new Date())}
                    >
                      {hasSubmission ? 'Resubmit' : 'Submit Work'}
                    </button>
                  </div>
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* Submission Modal */}
      {selectedAssignment && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="w-full max-w-lg bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden shadow-2xl relative">
            <div className="absolute top-0 left-0 right-0 h-[2px] bg-indigo-500"></div>
            <div className="p-6 flex items-center justify-between border-b border-slate-800">
              <div>
                <h3 className="font-bold text-white text-lg">Submit Assignment</h3>
                <p className="text-xs text-slate-400">{selectedAssignment.title}</p>
              </div>
              <button onClick={() => setSelectedAssignment(null)} className="text-slate-400 hover:text-white"><X className="h-5 w-5" /></button>
            </div>

            <form onSubmit={handleSubmissionSubmit} className="p-6 space-y-4">
              {error && <div className="p-3 bg-red-950/40 border border-red-800/60 rounded-xl text-red-200 text-xs">{error}</div>}

              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1.5">Your Response / Answer</label>
                <textarea
                  rows={6}
                  placeholder="Type your response here..."
                  value={submissionContent}
                  onChange={(e) => setSubmissionContent(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-sm text-slate-200 focus:border-indigo-500 outline-none resize-none"
                />
              </div>

              {/* Attachments */}
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-2">Attachments</label>
                
                {/* Upload Button */}
                <div className="relative border-2 border-dashed border-slate-800 hover:border-indigo-500/60 rounded-xl p-4 flex flex-col items-center justify-center text-center gap-2 transition-all">
                  <input
                    type="file"
                    accept=".pdf,.doc,.docx,.txt"
                    onChange={handleFileUpload}
                    disabled={uploading}
                    className="absolute inset-0 opacity-0 cursor-pointer disabled:cursor-not-allowed"
                  />
                  {uploading ? (
                    <RefreshCw className="h-6 w-6 animate-spin text-indigo-400" />
                  ) : (
                    <Upload className="h-6 w-6 text-slate-500" />
                  )}
                  <div>
                    <span className="text-xs font-semibold text-slate-300">Click to upload a file</span>
                    <span className="text-[10px] text-slate-500 block mt-0.5">Max size: 10MB &middot; Formats: PDF, DOC, DOCX, TXT</span>
                  </div>
                </div>

                {/* File List */}
                {attachedFiles.length > 0 && (
                  <div className="mt-3 space-y-2">
                    {attachedFiles.map((file) => (
                      <div key={file.id} className="flex justify-between items-center bg-slate-950/60 border border-slate-850 p-2.5 rounded-xl text-xs">
                        <div className="flex items-center gap-2 text-slate-300">
                          <FileText className="h-4 w-4 text-indigo-400" />
                          <span className="font-medium line-clamp-1">{file.originalFileName || file.fileName}</span>
                        </div>
                        <div className="flex gap-2">
                          <button
                            type="button"
                            onClick={() => handleDownloadFile(file.id, file.originalFileName || file.fileName)}
                            className="p-1 hover:bg-slate-800 rounded text-slate-400 hover:text-white"
                          >
                            <Download className="h-3.5 w-3.5" />
                          </button>
                          <button
                            type="button"
                            onClick={() => handleDeleteFile(file.id)}
                            className="p-1 hover:bg-slate-800 rounded text-slate-400 hover:text-red-400"
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </button>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              <div className="flex gap-3 justify-end pt-4 border-t border-slate-800/80 mt-6">
                <button type="button" onClick={() => setSelectedAssignment(null)} className="px-4 py-2 border border-slate-800 text-slate-400 hover:text-white rounded-xl text-sm hover:bg-slate-800/40">Cancel</button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-sm font-semibold transition-all shadow-lg shadow-indigo-500/10 flex items-center gap-2"
                >
                  {submitting ? (
                    <RefreshCw className="h-4 w-4 animate-spin" />
                  ) : (
                    <>
                      <Send className="h-3.5 w-3.5" />
                      Submit Work
                    </>
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
