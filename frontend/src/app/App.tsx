import { useEffect, useState } from 'react';
import { BrowserRouter, Link, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import {
  App as AntApp,
  Avatar,
  Badge,
  Button,
  ConfigProvider,
  Dropdown,
  Layout,
  Menu,
  Space,
  Spin
} from 'antd';
import type { MenuProps } from 'antd';
import {
  BellOutlined,
  CalendarOutlined,
  FileTextOutlined,
  HeartOutlined,
  HomeOutlined,
  LogoutOutlined,
  MessageOutlined,
  SearchOutlined,
  SettingOutlined,
  UserOutlined
} from '@ant-design/icons';
import { AuthProvider, useAuth } from './AuthContext';
import { ProtectedRoute, RoleGuard } from './guards';
import { AppLayout } from '../layouts/AppLayout';
import { Sidebar } from '../layouts/Sidebar';
import { tenantApi } from '../services/tenant';
import { landlordAdminApi } from '../services/landlordAdmin';
import { HomePage, PropertyListPage, PropertyDetailPage } from '../features/public/PropertyPages';
import { LoginPage, RegisterPage, ProfilePage } from '../features/auth/AuthPages';
import {
  FavoritesPage,
  BookingsPage,
  ContractsPage,
  InvoicesPage,
  NotificationsPage
} from '../features/tenant/TenantPages';
import { ChatPage } from '../features/tenant/ChatPage';
import {
  LandlordDashboard,
  LandlordProperties,
  LandlordBookings,
  LandlordContracts,
  LandlordInvoices,
  LandlordNotifications,
  LandlordChat,
  AdminDashboard,
  AdminUsers,
  AdminModeration
} from '../features/landlord-admin/Workspace';

const theme = {
  token: {
    colorPrimary: '#176b57',
    colorInfo: '#176b57',
    colorSuccess: '#23865b',
    colorWarning: '#d99528',
    colorError: '#c64d4d',
    borderRadius: 12,
    fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
    controlHeight: 42
  },
  components: {
    Button: { fontWeight: 600 },
    Card: { borderRadiusLG: 18 },
    Menu: { itemBorderRadius: 10, horizontalItemSelectedColor: '#176b57' },
    Table: { headerBg: '#f5f8f6', rowHoverBg: '#f7faf8' }
  }
};

function ForbiddenPage() {
  return (
    <main className="page status-page">
      <div className="status-page-card">
        <span className="status-code">403</span>
        <h1>Không đủ quyền truy cập</h1>
        <p>Tài khoản hiện tại không được phép xem trang này.</p>
        <Button type="primary" href="/">Về trang chủ</Button>
      </div>
    </main>
  );
}

function NotFoundPage() {
  return (
    <main className="page status-page">
      <div className="status-page-card">
        <span className="status-code">404</span>
        <h1>Không tìm thấy trang</h1>
        <p>Đường dẫn có thể đã thay đổi hoặc không tồn tại.</p>
        <Button type="primary" href="/">Về trang chủ</Button>
      </div>
    </main>
  );
}

function Shell() {
  const { user, loading, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [unread, setUnread] = useState(0);
  const isTenant = !!user?.roles.includes('Tenant');
  const isLandlord = !!user?.roles.includes('Landlord');
  const isAdmin = !!user?.roles.includes('Admin');
  const rolesKey = user?.roles.join('|') ?? '';
  const notificationPath = isLandlord ? '/landlord/notifications' : '/thong-bao';

  useEffect(() => {
    let active = true;
    if (!user) {
      setUnread(0);
      return () => { active = false; };
    }

    const request = isLandlord
      ? landlordAdminApi.unreadCount()
      : isTenant
        ? tenantApi.unreadCount()
        : null;

    if (!request) {
      setUnread(0);
      return () => { active = false; };
    }

    request
      .then(response => { if (active) setUnread(response.data.unreadCount); })
      .catch(() => { if (active) setUnread(0); });

    return () => { active = false; };
  }, [user?.userId, rolesKey, isLandlord, isTenant]);

  const landlordLinks: MenuProps['items'] = isLandlord ? [{
    key: 'landlord-group',
    icon: <HomeOutlined />,
    label: 'Khu vực chủ nhà',
    children: [
      { key: '/landlord', label: <Link to="/landlord">Tổng quan</Link> },
      { key: '/landlord/properties', label: <Link to="/landlord/properties">Tin đăng</Link> },
      { key: '/landlord/bookings', label: <Link to="/landlord/bookings">Yêu cầu thuê</Link> },
      { key: '/landlord/contracts', label: <Link to="/landlord/contracts">Hợp đồng</Link> },
      { key: '/landlord/invoices', label: <Link to="/landlord/invoices">Hóa đơn</Link> },
      { key: '/landlord/chat', label: <Link to="/landlord/chat">Tin nhắn</Link> },
      { key: '/landlord/notifications', label: <Link to="/landlord/notifications">Thông báo</Link> }
    ]
  }] : [];

  const adminLinks: MenuProps['items'] = isAdmin ? [{
    key: 'admin-group',
    icon: <SettingOutlined />,
    label: 'Quản trị',
    children: [
      { key: '/admin', label: <Link to="/admin">Tổng quan</Link> },
      { key: '/admin/users', label: <Link to="/admin/users">Người dùng</Link> },
      { key: '/admin/moderation', label: <Link to="/admin/moderation">Kiểm duyệt tin</Link> }
    ]
  }] : [];

  const tenantLinks: MenuProps['items'] = isTenant ? [{
    key: 'tenant-group',
    icon: <UserOutlined />,
    label: 'Tài khoản của tôi',
    children: [
      { key: '/yeu-thich', icon: <HeartOutlined />, label: <Link to="/yeu-thich">Yêu thích</Link> },
      { key: '/dat-thue', icon: <CalendarOutlined />, label: <Link to="/dat-thue">Yêu cầu thuê</Link> },
      { key: '/hop-dong', icon: <FileTextOutlined />, label: <Link to="/hop-dong">Hợp đồng</Link> },
      { key: '/hoa-don', label: <Link to="/hoa-don">Hóa đơn</Link> },
      { key: '/tin-nhan', icon: <MessageOutlined />, label: <Link to="/tin-nhan">Tin nhắn</Link> },
      { key: '/thong-bao', icon: <BellOutlined />, label: <Link to="/thong-bao">Thông báo</Link> }
    ]
  }] : [];

  const items: MenuProps['items'] = [
    { key: '/', icon: <HomeOutlined />, label: <Link to="/">Trang chủ</Link> },
    { key: '/nha', icon: <SearchOutlined />, label: <Link to="/nha">Tìm nhà</Link> },
    ...landlordLinks,
    ...adminLinks,
    ...tenantLinks
  ];

  const routeKeys = [
    '/', '/nha', '/landlord', '/landlord/properties', '/landlord/bookings',
    '/landlord/contracts', '/landlord/invoices', '/landlord/chat',
    '/landlord/notifications', '/admin', '/admin/users', '/admin/moderation',
    '/yeu-thich', '/dat-thue', '/hop-dong', '/hoa-don', '/tin-nhan',
    '/thong-bao', '/ho-so'
  ];
  const selectedKey = routeKeys
    .filter(key => key === '/'
      ? location.pathname === '/'
      : location.pathname === key || location.pathname.startsWith(key + '/'))
    .sort((a, b) => b.length - a.length)[0];

  if (loading) {
    return <div className="center app-loading"><Spin size="large" /></div>;
  }

  const accountMenu: MenuProps['items'] = [
    { key: 'profile', icon: <UserOutlined />, label: 'Hồ sơ cá nhân' },
    ...(isLandlord ? [{ key: 'dashboard', icon: <HomeOutlined />, label: 'Dashboard chủ nhà' }] : []),
    ...(isAdmin ? [{ key: 'admin', icon: <SettingOutlined />, label: 'Dashboard Admin' }] : []),
    { type: 'divider' },
    { key: 'logout', icon: <LogoutOutlined />, label: 'Đăng xuất', danger: true }
  ];

  return (
    <AppLayout
      header={
        <Layout.Header className="header">
          <div className="header-inner">
            <Link to="/" className="brand" aria-label="NhàỞ - Trang chủ">
              <span className="brand-mark"><HomeOutlined /></span>
              <span>NhàỞ</span>
            </Link>

            <nav className="desktop-navigation" aria-label="Điều hướng chính">
              <Menu
                mode="horizontal"
                items={items}
                selectedKeys={selectedKey ? [selectedKey] : []}
                className="desktop-menu"
              />
            </nav>

            <div className="header-actions">
              <Sidebar items={items} selectedKey={selectedKey} />
              {user && (isTenant || isLandlord) && (
                <Badge count={unread} size="small" overflowCount={99}>
                  <Button
                    className="header-icon-button"
                    type="text"
                    icon={<BellOutlined />}
                    aria-label="Thông báo"
                    onClick={() => navigate(notificationPath)}
                  />
                </Badge>
              )}

              {user ? (
                <Dropdown
                  trigger={['click']}
                  menu={{
                    items: accountMenu,
                    onClick: ({ key }) => {
                      if (key === 'profile') navigate('/ho-so');
                      if (key === 'dashboard') navigate('/landlord');
                      if (key === 'admin') navigate('/admin');
                      if (key === 'logout') {
                        logout();
                        navigate('/', { replace: true });
                      }
                    }
                  }}
                >
                  <Button className="account-button" type="text">
                    <Avatar size="small" src={user.avatarUrl} icon={<UserOutlined />} />
                    <span className="account-name">{user.fullName}</span>
                  </Button>
                </Dropdown>
              ) : (
                <Space className="auth-actions" size={8}>
                  <Button type="text" onClick={() => navigate('/dang-nhap')}>Đăng nhập</Button>
                  <Button type="primary" onClick={() => navigate('/dang-ky')}>Đăng ký</Button>
                </Space>
              )}
            </div>
          </div>
        </Layout.Header>
      }
    >
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/nha" element={<PropertyListPage />} />
        <Route path="/nha/:id" element={<PropertyDetailPage />} />
        <Route path="/dang-nhap" element={<LoginPage />} />
        <Route path="/dang-ky" element={<RegisterPage />} />
        <Route path="/403" element={<ForbiddenPage />} />
        <Route element={<ProtectedRoute />}>
          <Route path="/ho-so" element={<ProfilePage />} />
          <Route element={<RoleGuard roles={['Landlord']} />}>
            <Route path="/landlord" element={<LandlordDashboard />} />
            <Route path="/landlord/properties" element={<LandlordProperties />} />
            <Route path="/landlord/bookings" element={<LandlordBookings />} />
            <Route path="/landlord/contracts" element={<LandlordContracts />} />
            <Route path="/landlord/invoices" element={<LandlordInvoices />} />
            <Route path="/landlord/chat" element={<LandlordChat />} />
            <Route path="/landlord/notifications" element={<LandlordNotifications />} />
          </Route>
          <Route element={<RoleGuard roles={['Admin']} />}>
            <Route path="/admin" element={<AdminDashboard />} />
            <Route path="/admin/users" element={<AdminUsers />} />
            <Route path="/admin/moderation" element={<AdminModeration />} />
          </Route>
          <Route element={<RoleGuard roles={['Tenant']} />}>
            <Route path="/yeu-thich" element={<FavoritesPage />} />
            <Route path="/dat-thue" element={<BookingsPage />} />
            <Route path="/hop-dong" element={<ContractsPage />} />
            <Route path="/hoa-don" element={<InvoicesPage />} />
            <Route path="/thong-bao" element={<NotificationsPage />} />
            <Route path="/tin-nhan" element={<ChatPage />} />
          </Route>
        </Route>
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </AppLayout>
  );
}

export function App() {
  return (
    <ConfigProvider theme={theme}>
      <AntApp>
        <BrowserRouter>
          <AuthProvider>
            <Shell />
          </AuthProvider>
        </BrowserRouter>
      </AntApp>
    </ConfigProvider>
  );
}
