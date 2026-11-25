import { api } from './api';

export const tenantPaths = {
  favorites: '/api/favorites',
  bookings: '/api/bookings/mine',
  contracts: '/api/contracts',
  invoices: '/api/invoices',
  notifications: '/api/notifications',
  conversations: '/api/chat/conversations'
} as const;

export const tenantApi = {
  unreadCount: () => api.get('/api/notifications/unread-count'),
  properties: (params: Record<string, unknown>) => api.get('/api/properties', { params }),
  property: (id: string) => api.get('/api/properties/' + id),
  list: (path: string) => api.get(path, { params: { page: 1, pageSize: 20 } }),
  addFavorite: (id: string) => api.post('/api/favorites/' + id),
  removeFavorite: (id: string) => api.delete('/api/favorites/' + id),
  createBooking: (data: unknown) => api.post('/api/bookings', data),
  cancelBooking: (id: string) => api.patch('/api/bookings/' + id + '/cancel'),
  startConversation: (id: string) => api.post('/api/chat/properties/' + id + '/conversation'),
  signContract: (id: string) => api.post('/api/contracts/' + id + '/sign'),
  invoice: (id: string) => api.get('/api/invoices/' + id),
  checkout: (id: string) => api.post('/api/invoices/' + id + '/sepay-checkout'),
  manualPayment: (id: string, data: unknown) => api.post('/api/invoices/' + id + '/payments', data),
  readAll: () => api.patch('/api/notifications/read-all'),
  readNotification: (id: string) => api.patch('/api/notifications/' + id + '/read'),
  messages: (id: string) => api.get('/api/chat/conversations/' + id + '/messages', { params: { page: 1, pageSize: 50 } }),
  markChatRead: (id: string) => api.patch('/api/chat/conversations/' + id + '/read'),
  sendMessage: (id: string, content: string) => api.post('/api/chat/conversations/' + id + '/messages', { content }),
  uploadAttachment: (id: string, file: File) => { const body = new FormData(); body.append('file', file); return api.post('/api/chat/conversations/' + id + '/attachments', body, { headers: { 'Content-Type': 'multipart/form-data' } }); }
};
