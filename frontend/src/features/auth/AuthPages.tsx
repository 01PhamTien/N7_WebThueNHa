import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { App as AntApp, Avatar, Button, Card, Col, Empty, Form, Input, List, Rate, Row, Space, Spin, Typography } from 'antd';
import {
  HeartOutlined,
  HomeOutlined,
  SafetyCertificateOutlined,
  SearchOutlined,
  UserOutlined
} from '@ant-design/icons';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../app/AuthContext';
import { errorText } from '../../services/api';
import { tenantApi } from '../../services/tenant';
import { Page } from '../../components/Shared';
import type { LoginRequest, RegisterRequest, MyReviewItem } from '../../types/api';

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
      // Tài khoản quản trị/chủ nhà luôn vào đúng khu vực quản lý,
      // không dùng đường dẫn tenant cũ còn lưu trong state của lần đăng nhập trước.
      const roleHome = user.roles.includes('Admin')
        ? '/admin'
        : user.roles.includes('Landlord')
          ? '/landlord'
          : null;
      const from = (location.state as { from?: string } | null)?.from;
      navigate(roleHome || from || '/', { replace: true });
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
  const [reviews, setReviews] = useState<MyReviewItem[]>([]);
  const [reviewsBusy, setReviewsBusy] = useState(false);
  const [form] = Form.useForm<{ fullName: string; phoneNumber?: string; avatarUrl?: string }>();

  useEffect(() => {
    form.setFieldsValue({
      fullName: user?.fullName || '',
      phoneNumber: user?.phoneNumber || '',
      avatarUrl: user?.avatarUrl || ''
    });
  }, [form, user?.userId, user?.fullName, user?.phoneNumber, user?.avatarUrl]);

  useEffect(() => {
    if (!user?.roles.includes('Tenant')) {
      setReviews([]);
      return;
    }

    let active = true;
    setReviewsBusy(true);
    tenantApi.myReviews()
      .then(response => { if (active) setReviews(response.data || []); })
      .catch(() => { if (active) setReviews([]); })
      .finally(() => { if (active) setReviewsBusy(false); });
    return () => { active = false; };
  }, [user?.userId, user?.roles.join('|')]);

  const submit = async (values: { fullName: string; phoneNumber?: string; avatarUrl?: string }) => {
    setSubmitting(true);
    try {
      const { authService } = await import('../../services/auth');
      await authService.updateProfile({
        fullName: values.fullName.trim(),
        phoneNumber: values.phoneNumber?.trim() || undefined,
        avatarUrl: values.avatarUrl?.trim() || undefined
      });
      await refresh();
      message.success('Thông tin cá nhân đã được cập nhật.');
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setSubmitting(false);
    }
  };

  const roleLabels: Record<string, string> = {
    Admin: 'Quản trị viên',
    Landlord: 'Chủ nhà',
    Tenant: 'Người thuê'
  };

  return (
    <Page title="Hồ sơ cá nhân" description="Cập nhật thông tin liên hệ và xem lại các đánh giá bạn đã gửi.">
      <Card className="profile-card" bordered={false}>
        <div className="profile-card-heading">
          <Avatar
            className="profile-avatar"
            size={56}
            src={user?.avatarUrl || undefined}
            icon={<UserOutlined />}
          />
          <div>
            <strong>{user?.fullName || 'Người dùng NhàỞ'}</strong>
            <p>{user?.email} · {(user?.roles || []).map(role => roleLabels[role] || role).join(', ')}</p>
          </div>
        </div>

        <Form form={form} layout="vertical" requiredMark={false} onFinish={submit}>
          <Row gutter={[16, 0]}>
            <Col xs={24} md={12}>
              <Form.Item
                name="fullName"
                label="Họ và tên"
                rules={[
                  { required: true, message: 'Vui lòng nhập họ tên.' },
                  { min: 2, max: 150, message: 'Họ tên cần từ 2 đến 150 ký tự.' }
                ]}
              >
                <Input autoComplete="name" placeholder="Nhập họ và tên" />
              </Form.Item>
            </Col>
            <Col xs={24} md={12}>
              <Form.Item label="Địa chỉ email">
                <Input value={user?.email || ''} disabled />
              </Form.Item>
            </Col>
            <Col xs={24} md={12}>
              <Form.Item
                name="phoneNumber"
                label="Số điện thoại"
                rules={[{
                  validator: (_, value) =>
                    !value || /^[0-9+().\-\s]{8,20}$/.test(value)
                      ? Promise.resolve()
                      : Promise.reject(new Error('Số điện thoại chưa đúng định dạng.'))
                }]}
              >
                <Input autoComplete="tel" placeholder="Nhập số điện thoại" />
              </Form.Item>
            </Col>
            <Col xs={24} md={12}>
              <Form.Item
                name="avatarUrl"
                label="Ảnh đại diện (không bắt buộc)"
                rules={[{ type: 'url', message: 'Vui lòng nhập đường dẫn ảnh hợp lệ.' }]}
              >
                <Input placeholder="Đường dẫn ảnh HTTPS" />
              </Form.Item>
            </Col>
          </Row>
          <div className="profile-form-actions">
            <Space wrap>
              <Button onClick={() => form.setFieldsValue({
                fullName: user?.fullName || '',
                phoneNumber: user?.phoneNumber || '',
                avatarUrl: user?.avatarUrl || ''
              })}>
                Khôi phục
              </Button>
              <Button type="primary" htmlType="submit" loading={submitting}>
                Lưu thay đổi
              </Button>
            </Space>
          </div>
        </Form>
      </Card>

      {user?.roles.includes('Tenant') && (
        <Card className="profile-reviews-card" title="Đánh giá của tôi" bordered={false}>
          {reviewsBusy ? <Spin /> : reviews.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Bạn chưa gửi đánh giá nào. Sau khi kết thúc hợp đồng thuê, bạn có thể đánh giá căn nhà." />
          ) : (
            <List
              dataSource={reviews}
              renderItem={review => (
                <List.Item>
                  <List.Item.Meta
                    title={<Link to={'/nha/' + review.propertyId}>{review.propertyTitle}</Link>}
                    description={
                      <div>
                        <Rate disabled value={review.rating} />
                        {review.comment && <p>{review.comment}</p>}
                        <Typography.Text type="secondary">{new Date(review.createdAt).toLocaleDateString('vi-VN')}</Typography.Text>
                      </div>
                    }
                  />
                </List.Item>
              )}
            />
          )}
        </Card>
      )}
    </Page>
  );
}
