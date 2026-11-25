import { api, TOKEN_KEY } from './api';

export const authService = {
  login: async (data: { email: string; password: string }) => {
    const result = (await api.post('/api/auth/login', data)).data;
    sessionStorage.setItem(TOKEN_KEY, result.accessToken);
    return result;
  },
  register: (data: unknown) => api.post('/api/auth/register', data),
  me: () => api.get('/api/auth/me'),
  updateProfile: (data: unknown) => api.put('/api/auth/profile', data)
};
