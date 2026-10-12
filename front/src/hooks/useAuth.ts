export type User = {
  id: string;
  name?: string;
  surname?: string;
  email: string;
  roles: string[];
};

export interface AuthState {
  token: string | null;
  user: User | null;
  setToken: (token: string | null) => void;
  setUser: (user: User | null) => void;
  logout: () => void;
}

import { create } from 'zustand';

export const useAuthStore = create<AuthState>((set) => ({
  token: localStorage.getItem('token'),
  user: localStorage.getItem('user') ? JSON.parse(localStorage.getItem('user')!) : null,
  setToken: (token) => {
    if (token) localStorage.setItem('token', token); else localStorage.removeItem('token');
    set({ token });
  },
  setUser: (user) => {
    if (user) localStorage.setItem('user', JSON.stringify(user)); else localStorage.removeItem('user');
    set({ user });
  },
  logout: () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    set({ token: null, user: null });
  },
}));
