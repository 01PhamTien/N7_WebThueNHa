import { useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { Avatar, Button, Dropdown, Layout, Menu, Space, Typography } from 'antd';
import type { MenuProps } from 'antd';
import {
  BellOutlined,
  CalendarOutlined,
  CreditCardOutlined,
  DashboardOutlined,
  FileSearchOutlined,
  FileTextOutlined,
  HomeOutlined,
  LogoutOutlined,
  MessageOutlined,
  TeamOutlined,
  UserOutlined
} from '@ant-design/icons';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../app/AuthContext';

const { Sider, Header, Content, Footer } = Layout;

export function ManagementLayout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [collapsed, setCollapsed] = useState(false);
  const isAdminArea = location.pathname.startsWith('/admin');
  const title = isAdminArea ? 'Trung tâm quản trị' : 'Trung tâm chủ nhà';

  const menuItems = useMemo<NonNullable<MenuProps['items']>>(() => isAdminArea ? [
    { key: '/admin', icon: <DashboardOutlined />, label: <Link to="/admin">Tổng quan</Link> },
    { type: 'divider' },
    { key: '/admin/users', icon: <TeamOutlined />, label: <Link to="/admin/users">Quản lý người dùng</Link> },
    { key: '/admin/moderation', icon: <FileSearchOutlined />, label: <Link to="/admin/moderation">Tin đăng & kiểm duyệt</Link> }
  ] : [
    { key: '/landlord', icon: <DashboardOutlined />, label: <Link to="/landlord">Tổng quan</Link> },
    { type: 'divider' },
    { key: '/landlord/properties', icon: <HomeOutlined />, label: <Link to="/landlord/properties">Quản lý tin đăng</Link> },
    { key: '/landlord/bookings', icon: <CalendarOutlined />, label: <Link to="/landlord/bookings">Yêu cầu thuê</Link> },
    { key: '/landlord/contracts', icon: <FileTextOutlined />, label: <Link to="/landlord/contracts">Hợp đồng</Link> },
    { key: '/landlord/invoices', icon: <CreditCardOutlined />, label: <Link to="/landlord/invoices">Hóa đơn & thanh toán</Link> },
    { key: '/landlord/chat', icon: <MessageOutlined />, label: <Link to="/landlord/chat">Tin nhắn</Link> },
    { key: '/landlord/notifications', icon: <BellOutlined />, label: <Link to="/landlord/notifications">Thông báo</Link> }
  ], [isAdminArea]);

  const selectedKey = [...(isAdminArea
    ? ['/admin/users', '/admin/moderation', '/admin']
    : ['/landlord/properties', '/landlord/bookings', '/landlord/contracts', '/landlord/invoices', '/landlord/chat', '/landlord/notifications', '/landlord'])]
    .sort((a, b) => b.length - a.length)
    .find(key => location.pathname === key || (key !== '/admin' && key !== '/landlord' && location.pathname.startsWith(key + '/')))
    || (isAdminArea ? '/admin' : '/landlord');

  const accountItems: MenuProps['items'] = [
    { key: 'profile', icon: <UserOutlined />, label: 'Hồ sơ cá nhân' },
    { key: 'site', icon: <HomeOutlined />, label: 'Về trang người dùng' },
    { type: 'divider' },
    { key: 'logout', icon: <LogoutOutlined />, label: 'Đăng xuất', danger: true }
  ];

  return (
    <Layout className="management-layout" hasSider>
      <Sider
        className="management-sider"
        width={258}
        collapsedWidth={76}
        collapsible
        collapsed={collapsed}
        onCollapse={setCollapsed}
        breakpoint="lg"
      >
        <Link to={isAdminArea ? '/admin' : '/landlord'} className="management-brand">
          <span className="management-brand-mark"><HomeOutlined /></span>
          {!collapsed && <span><strong>NhàỞ</strong><small>{isAdminArea ? 'ADMIN CONSOLE' : 'LANDLORD CONSOLE'}</small></span>}
        </Link>
        {!collapsed && <div className="management-nav-caption">{isAdminArea ? 'QUẢN TRỊ HỆ THỐNG' : 'QUẢN LÝ CHO THUÊ'}</div>}
        <Menu
          className="management-menu"
          mode="inline"
          items={menuItems}
          selectedKeys={[selectedKey]}
          inlineCollapsed={collapsed}
        />
        <div className="management-sider-bottom">
          <Button block type="text" icon={<HomeOutlined />} onClick={() => navigate('/')}>
            {!collapsed && 'Xem website'}
          </Button>
        </div>
      </Sider>

      <Layout className="management-main">
        <Header className="management-header">
          <div className="management-header-title">
            <Typography.Text type="secondary">{title}</Typography.Text>
            <Typography.Title level={4}>
              {isAdminArea ? 'Quản trị NhàỞ' : 'Quản lý hoạt động cho thuê'}
            </Typography.Title>
          </div>
          <Space size={12}>
            <div className="management-user-meta">
              <strong>{user?.fullName}</strong>
              <span>{isAdminArea ? 'Quản trị viên' : 'Chủ nhà'}</span>
            </div>
            <Dropdown
              trigger={['click']}
              menu={{
                items: accountItems,
                onClick: ({ key }) => {
                  if (key === 'profile') navigate('/ho-so');
                  if (key === 'site') navigate('/');
                  if (key === 'logout') {
                    logout();
                    navigate('/', { replace: true });
                  }
                }
              }}
            >
              <Button className="management-avatar-button" type="text">
                <Avatar src={user?.avatarUrl} icon={<UserOutlined />} />
              </Button>
            </Dropdown>
          </Space>
        </Header>

        <Content className="management-content">
          {children}
        </Content>
        <Footer className="management-footer">© {new Date().getFullYear()} NhàỞ · Khu vực quản lý</Footer>
      </Layout>
    </Layout>
  );
}
