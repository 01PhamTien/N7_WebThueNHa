import{createContext,useCallback,useContext,useEffect,useMemo,useState,type ReactNode}from'react';import{TOKEN_KEY}from'../services/api';import{authService}from'../services/auth';import type{CurrentUser,LoginRequest,RegisterRequest}from'../types/api';
interface AuthContextValue{user:CurrentUser|null;loading:boolean;login:(data:LoginRequest)=>Promise<CurrentUser>;register:(data:RegisterRequest)=>Promise<void>;logout:()=>void;refresh:()=>Promise<void>}
const Context=createContext<AuthContextValue|null>(null);
export function AuthProvider({children}:{children:ReactNode}){const[user,setUser]=useState<CurrentUser|null>(null);const[loading,setLoading]=useState(true);
const clear=useCallback(()=>{sessionStorage.removeItem(TOKEN_KEY);setUser(null)},[]);
const refresh=useCallback(async()=>{if(!sessionStorage.getItem(TOKEN_KEY)){setUser(null);return}try{const response=await authService.me();setUser(response.data)}catch{clear()}},[clear]);
useEffect(()=>{let mounted=true;const unauthorized=()=>{if(mounted){setUser(null);setLoading(false)}};window.addEventListener('web2:unauthorized',unauthorized);void refresh().finally(()=>{if(mounted)setLoading(false)});return()=>{mounted=false;window.removeEventListener('web2:unauthorized',unauthorized)}},[refresh]);
const login=useCallback(async(data:LoginRequest)=>{await authService.login(data);const response=await authService.me();setUser(response.data);return response.data},[]);
const register=useCallback(async(data:RegisterRequest)=>{await authService.register(data)},[]);
const value=useMemo(()=>({user,loading,login,register,logout:clear,refresh}),[user,loading,login,register,clear,refresh]);
return <Context.Provider value={value}>{children}</Context.Provider>}
export function useAuth(){const value=useContext(Context);if(!value)throw new Error('useAuth phải nằm trong AuthProvider');return value}
