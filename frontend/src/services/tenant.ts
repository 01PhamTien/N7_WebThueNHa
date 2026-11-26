import{api}from'./api';import type{PageResult,PropertyItem,FavoriteItem,BookingItem,ContractItem,ContractDetail,InvoiceItem,InvoiceDetail,NotificationItem,ConversationItem,ChatMessage,CurrentUser,PropertyReviewSummary,MyReviewItem}from'../types/api';

// Booking API serializes enums as numbers; normalize at the tenant API boundary.
const bookingStatuses = ['Pending', 'Accepted', 'Rejected', 'Cancelled', 'Expired'] as const;
function normalizeBooking(item: BookingItem): BookingItem {
 const raw = String(item.status);
 const status = /^[0-4]$/.test(raw) ? bookingStatuses[Number(raw)] : bookingStatuses.find(value => value === raw);
 return {...item, status: status ?? 'Unknown'};
}
export const tenantApi={
 unreadCount:()=>api.get<{unreadCount:number}>('/api/notifications/unread-count'),
 properties:(params:Record<string,string|number|undefined>)=>api.get<PageResult<PropertyItem>>('/api/properties',{params}),
 property:(id:string)=>api.get<PropertyItem>('/api/properties/'+id),
 propertyReviews:(id:string)=>api.get<PropertyReviewSummary>('/api/reviews/properties/'+id),
 myReviews:()=>api.get<MyReviewItem[]>('/api/reviews/mine'),
 createReview:(contractId:string,data:{rating:number;comment?:string})=>api.post('/api/reviews/contracts/'+contractId,data),
 favorites:(page:number,pageSize:number)=>api.get<PageResult<FavoriteItem>>('/api/favorites',{params:{page,pageSize}}),
 favoriteStatus:(id:string)=>api.get<{propertyId:string;isFavorite:boolean}>('/api/favorites/'+id+'/status'),
 addFavorite:(id:string)=>api.post('/api/favorites/'+id),
 removeFavorite:(id:string)=>api.delete('/api/favorites/'+id),
 bookings:(page:number,pageSize:number)=>api.get<PageResult<BookingItem>>('/api/bookings/mine',{params:{page,pageSize}}).then(response=>({...response,data:{...response.data,items:response.data.items.map(normalizeBooking)}})),
 booking:(id:string)=>api.get<BookingItem>('/api/bookings/'+id).then(response=>({...response,data:normalizeBooking(response.data)})),
 createBooking:(data:{propertyId:string;startDate:string;endDate:string;numberOfPeople:number;message?:string})=>api.post('/api/bookings',data),
 cancelBooking:(id:string)=>api.patch<BookingItem>('/api/bookings/'+id+'/cancel').then(response=>({...response,data:normalizeBooking(response.data)})),
 moveOutRequests:()=>api.get<MoveOutRequestItem[]>('/api/move-outs'),
 createMoveOut:(data:{contractId:string;requestedMoveOutDate:string;reason:string;tenantNotes?:string})=>api.post('/api/move-outs',data),
 cancelMoveOut:(id:string)=>api.post('/api/move-outs/'+id+'/cancel'),
 contracts:(page:number,pageSize:number)=>api.get<PageResult<ContractItem>>('/api/contracts',{params:{page,pageSize}}),
 contract:(id:string)=>api.get<ContractDetail>('/api/contracts/'+id),
 signContract:(id:string)=>api.post('/api/contracts/'+id+'/sign'),
 invoices:(page:number,pageSize:number)=>api.get<PageResult<InvoiceItem>>('/api/invoices',{params:{page,pageSize}}),
 invoice:(id:string)=>api.get<InvoiceDetail>('/api/invoices/'+id),
 checkout:(id:string)=>api.post<{checkoutUrl?:string}>('/api/invoices/'+id+'/sepay-checkout'),
 manualPayment:(id:string,data:{paymentMethod:number;amount:number;transactionCode?:string|null})=>api.post('/api/invoices/'+id+'/payments',data),
 notifications:(page:number,pageSize:number)=>api.get<PageResult<NotificationItem>>('/api/notifications',{params:{page,pageSize}}),
 readAll:()=>api.patch<{updatedCount:number}>('/api/notifications/read-all'),
 readNotification:(id:string)=>api.patch('/api/notifications/'+id+'/read'),
 conversations:(page:number,pageSize:number,includeArchived:boolean)=>api.get<{page:number;pageSize:number;totalItems:number;totalPages:number;hasPrevious:boolean;hasNext:boolean;items:ConversationItem[]}>('/api/chat/conversations',{params:{page,pageSize,includeArchived}}),
 messages:(id:string,page:number,pageSize:number)=>api.get<{page:number;pageSize:number;totalItems:number;totalPages:number;hasPrevious:boolean;hasNext:boolean;items:ChatMessage[]}>('/api/chat/conversations/'+id+'/messages',{params:{page,pageSize}}),
 startConversation:(id:string)=>api.post<{conversationId:string;created:boolean}>('/api/chat/properties/'+id+'/conversation'),
 markChatRead:(id:string)=>api.patch('/api/chat/conversations/'+id+'/read'),
 archiveConversation:(id:string,isArchived:boolean)=>api.patch('/api/chat/conversations/'+id+'/archive',{isArchived}),
 sendMessage:(id:string,content:string)=>api.post<{messageId:string;conversationId:string;senderId:string;senderName:string;content:string;messageType:string;sentAt:string}>('/api/chat/conversations/'+id+'/messages',{content}),
 downloadAttachment:(attachmentId:string)=>api.get<Blob>('/api/chat/attachments/'+attachmentId,{responseType:'blob'}),
 uploadAttachment:(id:string,file:File)=>{const body=new FormData();body.append('file',file);return api.post('/api/chat/conversations/'+id+'/attachments',body)}
};
