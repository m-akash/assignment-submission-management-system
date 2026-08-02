import axios from 'axios';

// Resolve base URL dynamically to handle Docker vs Local Dev port mapping automatically
let API_BASE_URL = process.env.NEXT_PUBLIC_API_URL;

if (!API_BASE_URL && typeof window !== 'undefined') {
  const isDockerCompose = window.location.port === '3000';
  const apiPort = isDockerCompose ? '5080' : '5269';
  API_BASE_URL = `${window.location.protocol}//${window.location.hostname}:${apiPort}`;
} else if (!API_BASE_URL) {
  API_BASE_URL = 'http://localhost:5269'; // Server-side render default
}

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Interceptor to inject JWT token
api.interceptors.request.use(
  (config) => {
    if (typeof window !== 'undefined') {
      const token = localStorage.getItem('token');
      if (token && config.headers) {
        config.headers.Authorization = `Bearer ${token}`;
      }
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Interceptor to handle auth errors and unpack custom envelope
api.interceptors.response.use(
  (response) => {
    if (response.data && response.data.success !== undefined) {
      return response.data;
    }
    return response;
  },
  (error) => {
    if (error.response) {
      const status = error.response.status;
      if (status === 401 && typeof window !== 'undefined') {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
        if (window.location.pathname !== '/login') {
          window.location.href = '/login';
        }
      }
      
      const apiError = error.response.data;
      if (apiError && apiError.success === false) {
        return Promise.reject(new Error(apiError.message || 'API request failed'));
      }
    }
    return Promise.reject(error);
  }
);
