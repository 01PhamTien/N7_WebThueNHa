import { useState } from 'react';
import type { ReactNode } from 'react';
import { App as AntApp, Button, Card, Form, Input } from 'antd';
import {
  HeartOutlined,
  HomeOutlined,
  SafetyCertificateOutlined,
  SearchOutlined
} from '@ant-design/icons';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../app/AuthContext';
import { errorText } from '../../services/api';
import { Page } from '../../components/Shared';
import type { LoginRequest, RegisterRequest } from '../../types/api';

function AuthFrame({
  eyebrow,
  title,
  description,
  cardTitle,
  cardDescription,
  children
}: {
  eyebrow: string;
  title: string;
  description: string;
  cardTitle: string;
  cardDescription: string;
  children: ReactNode;
}) {
  return (
    <main className="page auth-page">
      <div className="auth-layout">
        <section className="auth-intro">
          <Link to="/" className="auth-brand">
            <span className="auth-brand-mark"><HomeOutlined /></span>
            <span>NhàỞ</span>
          </Link>
          <span className="auth-eyebrow">{eyebrow}</span>
          <h1>{title}</h1>
          <p>{description}</p>
          <div className="auth-benefits">
            <div>
              <span><SearchOutlined /></span>
              <div><strong>Tìm kiếm dễ dàng</strong><small>Khám phá nơi ở phù hợp với nhu cầu.</small></div>
            </div>
            <div>
              <span><SafetyCertificateOutlined /></span>
              <div><strong>Quản lý rõ ràng</strong><small>Theo dõi yêu cầu thuê và hợp đồng.</small></div>
            </div>
            <div>
              <span><HeartOutlined /></span>
              <div><strong>Lưu căn nhà yêu thích</strong><small>Gom những lựa chọn tốt vào một nơi.</small></div>
            </div>
          </div>
        </section>

        <Card className="auth">
          <div className="auth-card-heading">
            <span>CHÀO MỪNG BẠN</span>
            <h2>{cardTitle}</h2>
            <p>{cardDescription}</p>
          </div>
          {children}
        </Card>
      </div>
    </main>
  );
}

export function LoginPage() {
  const { message } = AntApp.useApp();
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [submitting, setSubmitting] = useState(false);

  const submit = async (values: LoginRequest) => {
    setSubmitting(true);
    try {
      const user = await login(values);
      const from = (location.state as { from?: string } | null)?.from;
      navigate(
        from || (user.roles.includes('Admin') ? '/admin' : user.roles.includes('Landlord') ? '/landlord' : '/'),
        { replace: true }
      );
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthFrame
      eyebrow="NỀN TẢNG KẾT NỐI THUÊ NHÀ"
      title="Chạm đúng nơi, tìm đúng nhà."
      description="Đăng nhập để tiếp tục hành trình tìm kiếm không gian sống phù hợp và quản lý mọi thông tin thuê nhà thuận tiện hơn."
      cardTitle="Đăng nhập"
      cardDescription="Nhập thông tin tài khoản để tiếp tục."
    >
      <Form layout="vertical" requiredMark={false} onFinish={submit} size="large">
        <Form.Item
          name="email"
          label="Email"
          rules={[{ required: true, type: 'email', message: 'Vui lòng nhập email hợp lệ.' }]}
        >
          <Input autoComplete="email" placeholder="name@example.com" />
        </Form.Item>
        <Form.Item
          name="password"
          label="Mật khẩu"
          rules={[{ required: true, message: 'Vui lòng nhập mật khẩu.' }]}
        >
          <Input.Password autoComplete="current-password" placeholder="Nhập mật khẩu" />
        </Form.Item>
        <Button type="primary" htmlType="submit" block loading={submitting}>
          Đăng nhập
        </Button>
      </Form>
      <p className="auth-switch">Chưa có tài khoản? <Link to="/dang-ky">Tạo tài khoản mới</Link></p>
    </AuthFrame>
  );
}

export function RegisterPage() {
  const { message } = AntApp.useApp();
  const { register } = useAuth();
  const navigate = useNavigate();
  const [submitting, setSubmitting] = useState(false);

  const submit = async (values: RegisterRequest) => {
    setSubmitting(true);
    try {
      await register(values);
      message.success('Đăng ký thành công. Vui lòng đăng nhập.');
      navigate('/dang-nhap');
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthFrame
      eyebrow="BẮT ĐẦU CÙNG NHÀỞ"
      title="Một nơi ở phù hợp bắt đầu từ đây."
      description="Tạo tài khoản để lưu căn nhà yêu thích, gửi yêu cầu thuê và theo dõi hành trình thuê nhà của bạn."
      cardTitle="Tạo tài khoản"
      cardDescription="Chỉ mất một chút thời gian để bắt đầu."
    >
      <Form layout="vertical" requiredMark={false} onFinish={submit} size="large">
        <Form.Item
          name="fullName"
          label="Họ và tên"
          rules={[{ required: true, min: 2, max: 150, message: 'Họ tên cần từ 2 đến 150 ký tự.' }]}
        >
          <Input autoComplete="name" placeholder="Nguyễn Văn An" />
        </Form.Item>
        <Form.Item
          name="email"
          label="Email"
          rules={[{ required: true, type: 'email', message: 'Vui lòng nhập email hợp lệ.' }]}
        >
          <Input autoComplete="email" placeholder="name@example.com" />
        </Form.Item>
        <Form.Item name="phoneNumber" label="Số điện thoại">
          <Input autoComplete="tel" placeholder="Nhập số điện thoại (không bắt buộc)" />
        </Form.Item>
        <Form.Item
          name="password"
          label="Mật khẩu"
          rules={[{ required: true, min: 8, message: 'Mật khẩu cần ít nhất 8 ký tự.' }]}
        >
          <Input.Password autoComplete="new-password" placeholder="Tối thiểu 8 ký tự" />
        </Form.Item>
        <Button type="primary" htmlType="submit" block loading={submitting}>
          Đăng ký
        </Button>
      </Form>
      <p className="auth-switch">Đã có tài khoản? <Link to="/dang-nhap">Đăng nhập</Link></p>
    </AuthFrame>
  );
}

export function ProfilePage() {
  const { message } = AntApp.useApp();
  const { user, refresh } = useAuth();
  const [submitting, setSubmitting] = useState(false);

  const submit = async (values: { fullName: string; phoneNumber?: string; avatarUrl?: string }) => {
    setSubmitting(true);
    try {
      const { authService } = await import('../../services/auth');
      await authService.updateProfile(values);
      await refresh();
      message.success('Đã cập nhật hồ sơ.');
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Page title="Hồ sơ cá nhân" description="Cập nhật thông tin để việc liên hệ và quản lý thuê nhà thuận tiện hơn.">
      <Card className="profile-card">
        <div className="profile-card-heading">
          <span className="profile-avatar"><HomeOutlined /></span>
          <div>
            <strong>Thông tin tài khoản</strong>
            <p>Thông tin của bạn được sử dụng trong các giao dịch thuê nhà.</p>
          </div>
        </div>
        <Form
          layout="vertical"
          requiredMark={false}
          initialValues={{
            fullName: user?.fullName,
            phoneNumber: user?.phoneNumber,
            avatarUrl: user?.avatarUrl
          }}
          onFinish={submit}
        >
          <Form.Item name="fullName" label="Họ và tên" rules={[{ required: true, min: 2, message: 'Vui lòng nhập họ tên.' }]}>
            <Input size="large" />
          </Form.Item>
          <Form.Item label="Email">
            <Input size="large" value={user?.email} disabled />
          </Form.Item>
          <Form.Item name="phoneNumber" label="Số điện thoại">
            <Input size="large" autoComplete="tel" />
          </Form.Item>
          <Form.Item name="avatarUrl" label="Đường dẫn ảnh đại diện">
            <Input size="large" placeholder="https://..." />
          </Form.Item>
          <div className="profile-form-actions">
            <Button type="primary" htmlType="submit" loading={submitting}>Lưu thay đổi</Button>
          </div>
        </Form>
      </Card>
    </Page>
  );
}
