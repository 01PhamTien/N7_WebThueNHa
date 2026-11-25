import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Spin } from 'antd';
import { useAuth } from './AuthContext';

export function ProtectedRoute() {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading) return <div className="center"><Spin /></div>;
  return user
    ? <Outlet />
    : <Navigate to="/dang-nhap" state={{ from: location.pathname + location.search }} replace />;
}

export function RoleGuard({ roles }: { roles: string[] }) {
  const { user } = useAuth();

  if (user && user.roles.some(role => roles.includes(role)))
    return <Outlet />;

  // Nếu tài khoản quản trị/chủ nhà mở nhầm liên kết tenant cũ,
  // đưa về dashboard đúng vai trò thay vì dừng ở trang 403.
  const fallback = user?.roles.includes('Admin')
    ? '/admin'
    : user?.roles.includes('Landlord')
      ? '/landlord'
      : '/';

  return <Navigate to={fallback} replace />;
}
