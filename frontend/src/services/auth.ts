import{api,TOKEN_KEY}from'./api';import type{CurrentUser,LoginRequest,RegisterRequest}from'../types/api';
export const authService={
 login:async(data:LoginRequest)=>{const result=(await api.post<{accessToken:string}>('/api/auth/login',data)).data;sessionStorage.setItem(TOKEN_KEY,result.accessToken);return result},
 register:(data:RegisterRequest)=>api.post('/api/auth/register',data),
 me:()=>api.get<CurrentUser>('/api/auth/me'),
 updateProfile:(data:{fullName:string;phoneNumber?:string;avatarUrl?:string})=>api.put('/api/auth/profile',data)
};
