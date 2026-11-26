import { api } from './api';

export interface PageParams {
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount?: number;
  totalItems?: number;
  totalPages?: number;
  hasPrevious?: boolean;
  hasNext?: boolean;
}

export interface PropertyImageDto {
  id: string;
  imageUrl: string;
  isPrimary: boolean;
  displayOrder: number;
}

export interface PropertyDto {
  id: string;
  title: string;
  description?: string | null;
  propertyTypeId: number;
  propertyTypeName?: string;
  pricePerMonth: number;
  depositAmount: number;
  maxOccupants: number;
  furnishingDescription?: string | null;
  additionalFeesDescription?: string | null;
  area: number;
  bedrooms: number;
  bathrooms: number;
  addressLine: string;
  wardName?: string | null;
  provinceName: string;
  moderationStatus: string;
  listingStatus: string;
  rentalStatus?: 'Available' | 'PendingRequest' | 'Reserved' | 'Rented';
  images: PropertyImageDto[];
}

export interface PropertyRequest {
  propertyTypeId: number;
  title: string;
  description?: string;
  pricePerMonth: number;
  depositAmount: number;
  maxOccupants: number;
  furnishingDescription?: string;
  additionalFeesDescription?: string;
  area: number;
  bedrooms: number;
  bathrooms: number;
  addressLine: string;
  wardName?: string;
  provinceName: string;
  latitude?: number;
  longitude?: number;
}

export interface BookingDto {
  id: string;
  propertyId: string;
  propertyTitle: string;
  tenantId: string;
  tenantName: string;
  tenantEmail: string;
  startDate: string;
  endDate: string;
  numberOfPeople: number;
  monthlyRent: number;
  estimatedRent: number;
  fullMonths: number;
  extraDays: number;
  message?: string | null;
  status: number;
  respondedAt?: string | null;
  rejectionReason?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface ContractDto {
  id: string;
  contractCode: string;
  bookingId: string;
  propertyId: string;
  propertyTitle: string;
  landlordId: string;
  landlordName: string;
  tenantId: string;
  tenantName: string;
  startDate: string;
  endDate: string;
  monthlyRent: number;
  depositAmount: number;
  estimatedRent: number;
  paymentDueDay?: number;
  terms?: string | null;
  status: string;
  landlordSignedAt?: string | null;
  tenantSignedAt?: string | null;
  createdAt: string;
}

export interface ContractDetailDto {
  contract: ContractDto & { bookingId: string; landlordId: string; tenantId: string; paymentDueDay: number; terms?: string | null; signedAt?: string | null; contractUrl?: string | null };
  propertyTitle: string;
  propertyTypeName: string;
  propertyDescription?: string | null;
  addressLine: string;
  wardName?: string | null;
  provinceName: string;
  area: number;
  bedrooms: number;
  bathrooms: number;
  maxOccupants: number;
  furnishingDescription?: string | null;
  additionalFeesDescription?: string | null;
  amenities: string[];
  images: PropertyImageDto[];
  numberOfPeople: number;
  bookingMessage?: string | null;
  landlordName: string;
  landlordEmail?: string | null;
  landlordPhone?: string | null;
  tenantName: string;
  tenantEmail?: string | null;
  tenantPhone?: string | null;
}

export interface UpdateContractRequest {
  startDate: string;
  endDate: string;
  monthlyRent: number;
  depositAmount: number;
  paymentDueDay: number;
  terms?: string;
}

export interface LandlordFinancialSummaryDto {
  months: number; receivedAllTime: number; receivedThisMonth: number;
  rentReceived: number; depositReceived: number; otherReceived: number;
  outstandingAmount: number; overdueAmount: number; overdueCount: number;
  pendingPayments: number; pendingRequests: number; propertyCount: number;
  activeContracts: number; endingSoon: number;
  refundedAllTime:number; refundedThisMonth:number; netReceivedAllTime:number; netReceivedThisMonth:number;
  rentedPropertyCount:number; signingCount:number; dueSoonAmount:number; pendingMoveOuts:number; handoverDue:number; waitingMoveOutSignature:number; waitingTenantSignature:number; upcomingHandovers:number; openMoveOutCount:number;
  moveOutItems:Array<{id:string;propertyTitle:string;contractCode:string;requestedMoveOutDate:string;status:string;tenantName:string;landlordConfirmedAt?:string|null;tenantConfirmedAt?:string|null}>;
  overdueItems:Array<{id:string;invoiceCode:string;propertyTitle:string;dueDate:string;totalAmount:number;paid:number}>;
  trend: Array<{year:number;month:number;label:string;received:number;rent:number;other:number}>;
}

export interface InvoiceItemDto {
  id: string;
  description: string;
  quantity: number;
  unitPrice: number;
  amount: number;
}

export interface PaymentDto {
  id: string;
  invoiceId: string;
  payerId: string;
  amount: number;
  paymentMethod: string;
  status: string;
  paymentProvider?: string | null;
  transactionCode?: string | null;
  paidAt?: string | null;
  failureReason?: string | null;
  reviewedById?: string | null;
  reviewedAt?: string | null;
  createdAt: string;
}

export interface InvoiceListDto {
  id: string;
  invoiceCode: string;
  contractId: string;
  contractCode: string;
  propertyId: string;
  propertyTitle: string;
  invoiceType: string;
  periodStart?: string | null;
  periodEnd?: string | null;
  issueDate: string;
  dueDate: string;
  totalAmount: number;
  status: string;
  createdAt: string;
}

export interface InvoiceDetailDto extends InvoiceListDto {
  periodStart?: string | null;
  periodEnd?: string | null;
  items: InvoiceItemDto[];
  payments: PaymentDto[];
}

export interface CreateInvoiceItemRequest {
  description: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateInvoiceRequest {
  invoiceType: number;
  periodStart?: string | null;
  periodEnd?: string | null;
  issueDate: string;
  dueDate: string;
  items: CreateInvoiceItemRequest[];
}

export interface NotificationDto {
  id: string;
  title: string;
  content: string;
  isRead: boolean;
  createdAt: string;
}

export interface ConversationDto {
  conversationId: string;
  propertyId: string;
  propertyTitle: string;
  tenantId: string;
  tenantName: string;
  landlordId: string;
  landlordName: string;
  lastMessageContent?: string | null;
  lastMessageAt?: string | null;
  unreadCount: number;
  createdAt: string;
  isArchived: boolean;
}

export interface ChatAttachmentDto {
  attachmentId: string;
  fileName: string;
  contentType?: string | null;
  fileSize: number;
  downloadUrl: string;
}

export interface ChatMessageDto {
  messageId: string;
  senderId: string;
  senderName: string;
  content?: string | null;
  messageType: string;
  sentAt: string;
  editedAt?: string | null;
  isMine: boolean;
  attachments: ChatAttachmentDto[];
}

export interface AdminDashboardDto {
  totalUsers: number;
  activeUsers: number;
  totalProperties: number;
  pendingProperties: number;
  publishedProperties: number;
  pendingBookings: number;
  activeContracts: number;
  overdueInvoices: number;
  totalBookings: number;
  totalContracts: number;
  totalInvoices: number;
  pendingPayments: number;
  successfulPayments: number;
  successfulPaymentAmount: number;
  totalReviews: number;
  totalModerationActions: number;
}

export interface AdminPaymentListDto {
  id: string;
  invoiceId: string;
  invoiceCode: string;
  contractCode: string;
  propertyTitle: string;
  payerId: string;
  payerName: string;
  amount: number;
  paymentMethod: string;
  status: string;
  paymentProvider?: string | null;
  transactionCode?: string | null;
  paidAt?: string | null;
  failureReason?: string | null;
  reviewedByName?: string | null;
  reviewedAt?: string | null;
  createdAt: string;
}

export interface AdminReviewDto {
  id: string;
  contractId: string;
  contractCode: string;
  propertyTitle: string;
  tenantName: string;
  rating: number;
  comment?: string | null;
  createdAt: string;
}

export interface AdminModerationLogDto {
  id: string;
  propertyId: string;
  propertyTitle: string;
  adminName: string;
  action: string;
  reason?: string | null;
  createdAt: string;
}

export interface CreateAdminUserRequest {
  fullName: string;
  email: string;
  password: string;
  phoneNumber?: string;
  avatarUrl?: string;
  roles: string[];
}

export interface UpdateAdminUserRequest {
  fullName: string;
  phoneNumber?: string;
  avatarUrl?: string;
}

export interface AdminUserDto {
  userId: string;
  fullName: string;
  email: string;
  phoneNumber?: string | null;
  avatarUrl?: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
  roles: string[];
}

export interface ModerationPropertyDto {
  id: string;
  title: string;
  landlordId?: string;
  landlordName: string;
  propertyTypeId?: number;
  propertyTypeName?: string;
  area?: number;
  bedrooms?: number;
  bathrooms?: number;
  latitude?: number | null;
  longitude?: number | null;
  provinceName: string;
  pricePerMonth: number;
  moderationStatus: number;
  listingStatus: number;
  createdAt?: string;
  description?: string | null;
  addressLine?: string;
  wardName?: string | null;
  images?: PropertyImageDto[];
  amenities?: string[];
  moderationLogs?: Array<{
    id: string;
    adminName: string;
    action: number;
    reason?: string | null;
    createdAt: string;
  }>;
}

export interface ChatPageParams extends PageParams {
  includeArchived?: boolean;
}

export const landlordAdminApi = {
  adminDashboard: () => api.get<AdminDashboardDto>('/api/admin/dashboard'),
  adminBookings: (params: PageParams & { status?: string; keyword?: string } = {}) =>
    api.get<PagedResult<BookingDto>>('/api/admin/operations/bookings', { params: { page: 1, pageSize: 10, ...params } }),
  adminContracts: (params: PageParams & { status?: string; keyword?: string } = {}) =>
    api.get<PagedResult<ContractDto>>('/api/admin/operations/contracts', { params: { page: 1, pageSize: 10, ...params } }),
  adminInvoices: (params: PageParams & { status?: string; type?: string; keyword?: string } = {}) =>
    api.get<PagedResult<InvoiceListDto>>('/api/admin/operations/invoices', { params: { page: 1, pageSize: 10, ...params } }),
  adminPayments: (params: PageParams & { status?: string; keyword?: string } = {}) =>
    api.get<PagedResult<AdminPaymentListDto>>('/api/admin/operations/payments', { params: { page: 1, pageSize: 10, ...params } }),
  adminReviews: (params: PageParams & { rating?: number; keyword?: string } = {}) =>
    api.get<PagedResult<AdminReviewDto>>('/api/admin/operations/reviews', { params: { page: 1, pageSize: 10, ...params } }),
  adminModerationLogs: (params: PageParams & { action?: string; keyword?: string } = {}) =>
    api.get<PagedResult<AdminModerationLogDto>>('/api/admin/operations/moderation-logs', { params: { page: 1, pageSize: 10, ...params } }),
  createAdminUser: (data: CreateAdminUserRequest) => api.post<AdminUserDto>('/api/admin/users', data),
  updateAdminUser: (id: string, data: UpdateAdminUserRequest) => api.put<AdminUserDto>('/api/admin/users/' + id, data),
  deleteAdminUser: (id: string) => api.delete<void>('/api/admin/users/' + id),

  landlordFinancialSummary: (months = 12) => api.get<LandlordFinancialSummaryDto>('/api/landlord/dashboard/financial-summary', { params: { months } }),

  myProperties: (params: PageParams & { status?: string; keyword?: string } = {}) =>
    api.get<PagedResult<PropertyDto>>('/api/properties/mine', { params: { page: 1, pageSize: 10, ...params } }),
  property: (id: string) => api.get<PropertyDto>('/api/properties/' + id),
  createProperty: (data: PropertyRequest) => api.post<PropertyDto>('/api/properties', data),
  updateProperty: (id: string, data: PropertyRequest) => api.put<PropertyDto>('/api/properties/' + id, data),
  updatePropertyStatus: (id: string, status: number) => api.patch<void>('/api/properties/' + id + '/status', { status }),
  addPropertyImage: (id: string, imageUrl: string) => api.post<PropertyImageDto>('/api/properties/' + id + '/images', { imageUrl }),
  uploadPropertyImage: (id: string, file: File) => { const data = new FormData(); data.append('file', file); return api.post<PropertyImageDto>('/api/properties/' + id + '/images/upload', data); },
  deletePropertyImage: (id: string, imageId: string) => api.delete<void>('/api/properties/' + id + '/images/' + imageId),
  primaryPropertyImage: (id: string, imageId: string) => api.put<void>('/api/properties/' + id + '/images/' + imageId + '/primary'),

  incomingBookings: (params: PageParams & { status?: string; keyword?: string } = {}) =>
    api.get<PagedResult<BookingDto>>('/api/bookings/incoming', { params: { page: 1, pageSize: 10, ...params } }),
  booking: (id: string) => api.get<BookingDto>('/api/bookings/' + id),
  acceptBooking: (id: string) => api.patch<BookingDto>('/api/bookings/' + id + '/accept'),
  rejectBooking: (id: string, reason: string) => api.patch<BookingDto>('/api/bookings/' + id + '/reject', { reason }),

  contracts: (params: PageParams & { status?: string; keyword?: string } = {}) =>
    api.get<PagedResult<ContractDto>>('/api/contracts', { params: { page: 1, pageSize: 10, ...params } }),
  contract: (id: string) => api.get<ContractDetailDto>('/api/contracts/' + id),
  createContract: (bookingId: string) => api.post<ContractDto>('/api/contracts/from-booking/' + bookingId),
  updateContract: (id: string, data: UpdateContractRequest) => api.put<ContractDto>('/api/contracts/' + id, data),
  signContract: (id: string) => api.post<ContractDto>('/api/contracts/' + id + '/sign'),
  submitContract: (id: string) => api.post<ContractDto>('/api/contracts/' + id + '/submit'),

  incomingPayments: (params: PageParams & { status?: string; keyword?: string } = {}) =>
    api.get<PagedResult<AdminPaymentListDto>>('/api/payments/incoming', { params: { page: 1, pageSize: 10, ...params } }),
  invoices: (params: PageParams & { status?: string; type?: string; keyword?: string } = {}) =>
    api.get<PagedResult<InvoiceListDto>>('/api/invoices', { params: { page: 1, pageSize: 10, ...params } }),
  invoice: (id: string) => api.get<InvoiceDetailDto>('/api/invoices/' + id),
  createInvoice: (contractId: string, data: CreateInvoiceRequest) =>
    api.post<InvoiceDetailDto>('/api/contracts/' + contractId + '/invoices', data),
  confirmPayment: (id: string) => api.patch<PaymentDto>('/api/payments/' + id + '/confirm'),
  rejectPayment: (id: string, reason: string) => api.patch<PaymentDto>('/api/payments/' + id + '/reject', { reason }),

  notifications: (params: PageParams & { isRead?: boolean; type?: string } = {}) =>
    api.get<PagedResult<NotificationDto>>('/api/notifications', { params: { page: 1, pageSize: 30, ...params } }),
  unreadCount: () => api.get<{ unreadCount: number }>('/api/notifications/unread-count'),
  readNotification: (id: string) => api.patch<void>('/api/notifications/' + id + '/read'),
  readAllNotifications: () => api.patch<{ updatedCount: number }>('/api/notifications/read-all'),

  conversations: (params: ChatPageParams = {}) =>
    api.get<PagedResult<ConversationDto>>('/api/chat/conversations', { params: { page: 1, pageSize: 30, ...params } }),
  archiveConversation: (id: string, isArchived: boolean) =>
    api.patch<{ conversationId: string; isArchived: boolean }>('/api/chat/conversations/' + id + '/archive', { isArchived }),
  messages: (id: string, params: PageParams = {}) =>
    api.get<PagedResult<ChatMessageDto>>('/api/chat/conversations/' + id + '/messages', { params: { page: 1, pageSize: 50, ...params } }),
  sendMessage: (id: string, content: string) => api.post<ChatMessageDto>('/api/chat/conversations/' + id + '/messages', { content }),
  uploadAttachment: (id: string, file: File) => {
    const data = new FormData();
    data.append('file', file);
    return api.post<ChatMessageDto>('/api/chat/conversations/' + id + '/attachments', data);
  },
  downloadAttachment: (attachmentId: string) =>
    api.get<Blob>('/api/chat/attachments/' + attachmentId, { responseType: 'blob' }),
  markChatRead: (id: string) => api.patch<void>('/api/chat/conversations/' + id + '/read'),

  adminUsers: (params: PageParams & { keyword?: string; isActive?: boolean } = {}) =>
    api.get<PagedResult<AdminUserDto>>('/api/admin/users', { params: { page: 1, pageSize: 10, ...params } }),
  adminUser: (id: string) => api.get<AdminUserDto>('/api/admin/users/' + id),
  updateUserStatus: (id: string, isActive: boolean) =>
    api.patch<{ userId: string; isActive: boolean }>('/api/admin/users/' + id + '/status', { isActive }),
  updateUserRoles: (id: string, roles: string[]) =>
    api.put<{ userId: string; roles: string[] }>('/api/admin/users/' + id + '/roles', { roles }),

  moderationProperties: (params: PageParams & { status?: string; listingStatus?: string; keyword?: string; province?: string; propertyTypeId?: number } = {}) =>
    api.get<PagedResult<ModerationPropertyDto>>('/api/admin/properties', { params: { page: 1, pageSize: 10, ...params } }),
  moderationDetail: (id: string) => api.get<ModerationPropertyDto>('/api/admin/properties/' + id),
  approveProperty: (id: string) => api.post<void>('/api/admin/properties/' + id + '/approve'),
  rejectProperty: (id: string, reason: string) => api.post<void>('/api/admin/properties/' + id + '/reject', { reason }),
  hideProperty: (id: string, reason: string) => api.post<void>('/api/admin/properties/' + id + '/hide', { reason }),
  restoreProperty: (id: string, reason: string) => api.post<void>('/api/admin/properties/' + id + '/restore', { reason })
};
