import{Navigate,Outlet,useLocation}from'react-router-dom';import{Spin}from'antd';import{useAuth}from'./AuthContext';
export function ProtectedRoute(){const{user,loading}=useAuth();const location=useLocation();if(loading)return <div className="center"><Spin/></div>;return user?<Outlet/>:<Navigate to="/dang-nhap" state={{from:location.pathname+location.search}} replace/>}
export function RoleGuard({roles}:{roles:string[]}){const{user}=useAuth();return user&&user.roles.some(role=>roles.includes(role))?<Outlet/>:<Navigate to="/403" replace/>}
