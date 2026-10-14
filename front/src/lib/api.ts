import { API_BASE_URL } from '@/config/env';

export class ApiError extends Error {
  status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const token = localStorage.getItem('token');
  const res = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...((init?.headers as Record<string, string>) || {}),
    },
  });

  if (res.status === 204) return undefined as T;
  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new ApiError(text || `Request failed ${res.status}`, res.status);
  }
  return res.json() as Promise<T>;
}

export async function apiFetchNullable<T>(path: string, init?: RequestInit): Promise<T | null> {
  try {
    return await apiFetch<T>(path, init);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}
