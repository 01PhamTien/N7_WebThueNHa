import { useEffect, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
  Empty,
  Form,
  Input,
  InputNumber,
  message,
  Modal,
  Row,
  Space,
  Spin,
  Tag
} from 'antd';
import {
  ArrowRightOutlined,
  EnvironmentOutlined,
  HomeOutlined,
  SearchOutlined,
  SafetyCertificateOutlined,
  MessageOutlined,
  FileTextOutlined
} from '@ant-design/icons';
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../app/AuthContext';
import { PaginationBar } from '../../components/PaginationBar';
import { errorText } from '../../services/api';
import { tenantApi } from '../../services/tenant';
import type { PageResult, PropertyItem } from '../../types/api';

const PAGE_SIZE = 12;

function PropertyCard({ item, to }: { item: PropertyItem; to: string }) {
  const imageUrl = item.images?.find(image => image.isPrimary)?.imageUrl || item.images?.[0]?.imageUrl;

  return (
    <Card
      hoverable
      className="property-card"
      cover={
        <Link to={to} className="property-cover">
          {imageUrl
            ? <img src={imageUrl} alt={item.title} loading="lazy" />
            : <div className="property-cover-placeholder"><HomeOutlined /><span>Nhà cho thuê</span></div>}
          <span className="property-cover-label">{item.propertyTypeName || 'Bất động sản'}</span>
        </Link>
      }
    >
      <div className="property-card-meta">
        <span><EnvironmentOutlined /> {item.provinceName || 'Chưa cập nhật khu vực'}</span>
      </div>
      <h3 className="property-card-title"><Link to={to}>{item.title}</Link></h3>
      <p className="property-address">{[item.addressLine, item.wardName, item.provinceName].filter(Boolean).join(', ')}</p>
      <div className="property-facts">
        <span>{item.area} m²</span>
        <span>{item.bedrooms} phòng ngủ</span>
        <span>{item.bathrooms} phòng tắm</span>
      </div>
      <div className="property-card-footer">
        <div>
          <strong className="price">{(item.pricePerMonth || 0).toLocaleString('vi-VN')} đ</strong>
          <small>/ tháng</small>
        </div>
        <Link to={to} className="property-detail-link">Chi tiết <ArrowRightOutlined /></Link>
      </div>
    </Card>
  );
}

export function HomePage() {
  const navigate = useNavigate();
  const [keyword, setKeyword] = useState('');
  const [items, setItems] = useState<PropertyItem[]>([]);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState('');
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;
    setBusy(true);
    setError('');

    tenantApi.properties({ page: 1, pageSize: 6 })
      .then(response => {
        if (active) setItems(response.data.items || []);
      })
      .catch(error => {
        if (active) {
          setItems([]);
          setError(errorText(error));
        }
      })
      .finally(() => {
        if (active) setBusy(false);
      });

    return () => { active = false; };
  }, [reloadKey]);

  const search = () => {
    const value = keyword.trim();
    navigate(value ? '/nha?keyword=' + encodeURIComponent(value) : '/nha');
  };

  return (
    <main className="home-page">
      <section className="hero">
        <div className="hero-inner">
          <div className="hero-copy">
            <Tag className="hero-eyebrow" icon={<HomeOutlined />}>Không gian sống dành cho bạn</Tag>
            <h1>Tìm ngôi nhà bạn muốn gọi là <span>nhà.</span></h1>
            <p className="hero-description">
              Khám phá phòng trọ, căn hộ và nhà cho thuê. Tìm kiếm theo khu vực,
              xem thông tin chi tiết và kết nối trực tiếp với chủ nhà.
            </p>
            <div className="hero-search">
              <Input
                size="large"
                value={keyword}
                onChange={event => setKeyword(event.target.value)}
                onPressEnter={search}
                prefix={<EnvironmentOutlined />}
                placeholder="Bạn muốn tìm nhà ở khu vực nào?"
                aria-label="Khu vực hoặc từ khóa tìm nhà"
              />
              <Button type="primary" size="large" icon={<SearchOutlined />} onClick={search}>
                Tìm nhà
              </Button>
            </div>
            <div className="hero-trust">
              <span><SafetyCertificateOutlined /> Thông tin rõ ràng</span>
              <span><MessageOutlined /> Kết nối chủ nhà</span>
              <span><FileTextOutlined /> Theo dõi yêu cầu thuê</span>
            </div>
          </div>

          <div className="hero-visual" aria-hidden="true">
            <div className="hero-visual-orbit orbit-one" />
            <div className="hero-visual-orbit orbit-two" />
            <div className="hero-visual-building">
              <div className="building-roof" />
              <div className="building-body">
                <div className="building-window" />
                <div className="building-window" />
                <div className="building-window" />
                <div className="building-window" />
                <div className="building-door" />
              </div>
            </div>
            <div className="hero-float-card hero-float-top">
              <span className="float-icon"><SearchOutlined /></span>
              <span><b>Tìm kiếm dễ dàng</b><small>Lọc theo nhu cầu của bạn</small></span>
            </div>
            <div className="hero-float-card hero-float-bottom">
              <span className="float-icon"><HomeOutlined /></span>
              <span><b>Chọn nơi phù hợp</b><small>Xem thông tin từng căn nhà</small></span>
            </div>
          </div>
        </div>
      </section>

      <section className="benefit-strip" aria-label="Các bước sử dụng">
        <div className="benefit-item">
          <span className="benefit-icon"><SearchOutlined /></span>
          <div><b>1. Tìm kiếm</b><small>Nhập khu vực và tiêu chí mong muốn</small></div>
        </div>
        <div className="benefit-item">
          <span className="benefit-icon"><HomeOutlined /></span>
          <div><b>2. Khám phá</b><small>Xem hình ảnh và thông tin căn nhà</small></div>
        </div>
        <div className="benefit-item">
          <span className="benefit-icon"><MessageOutlined /></span>
          <div><b>3. Kết nối</b><small>Gửi yêu cầu thuê và trao đổi với chủ nhà</small></div>
        </div>
      </section>

      <section className="home-section">
        <div className="section-heading">
          <div>
            <span className="section-kicker">KHÁM PHÁ</span>
            <h2>Tin đăng dành cho bạn</h2>
            <p>Tham khảo những căn nhà đang được giới thiệu trên hệ thống.</p>
          </div>
          <Button type="default" className="section-action" onClick={() => navigate('/nha')}>
            Xem tất cả <ArrowRightOutlined />
          </Button>
        </div>

        {error && (
          <Alert
            className="home-error"
            type="warning"
            showIcon
            message="Chưa tải được danh sách nhà"
            description={error}
            action={<Button size="small" onClick={() => setReloadKey(value => value + 1)}>Thử lại</Button>}
          />
        )}

        {busy ? (
          <div className="home-loading"><Spin size="large" /></div>
        ) : items.length === 0 ? (
          <div className="home-empty">
            <Empty description={error ? 'Không thể tải tin đăng lúc này.' : 'Chưa có tin đăng phù hợp.'}>
              <Button type="primary" onClick={() => navigate('/nha')}>Khám phá danh sách nhà</Button>
            </Empty>
          </div>
        ) : (
          <Row gutter={[22, 22]} className="property-grid">
            {items.map(item => (
              <Col xs={24} sm={12} lg={8} key={item.id}>
                <PropertyCard item={item} to={'/nha/' + item.id} />
              </Col>
            ))}
          </Row>
        )}

        <div className="home-bottom-cta">
          <div>
            <span className="section-kicker">BẮT ĐẦU NGAY</span>
            <h2>Chưa tìm thấy căn nhà phù hợp?</h2>
            <p>Thử thay đổi từ khóa hoặc mở toàn bộ danh sách tin đăng.</p>
          </div>
          <Button type="primary" size="large" onClick={() => navigate('/nha')}>
            Khám phá tất cả nhà <ArrowRightOutlined />
          </Button>
        </div>
      </section>
    </main>
  );
}

export function PropertyListPage() {
  const [params, setParams] = useSearchParams();
  const [items, setItems] = useState<PropertyItem[]>([]);
  const [meta, setMeta] = useState<PageResult<PropertyItem> | null>(null);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState('');
  const [keyword, setKeyword] = useState(params.get('keyword') || '');
  const [province, setProvince] = useState(params.get('province') || '');
  const [minPrice, setMinPrice] = useState<number | undefined>(
    params.get('minPrice') ? Number(params.get('minPrice')) : undefined
  );
  const [maxPrice, setMaxPrice] = useState<number | undefined>(
    params.get('maxPrice') ? Number(params.get('maxPrice')) : undefined
  );
  const page = Math.max(1, Number(params.get('page') || 1));

  useEffect(() => {
    setKeyword(params.get('keyword') || '');
    setProvince(params.get('province') || '');
    setMinPrice(params.get('minPrice') ? Number(params.get('minPrice')) : undefined);
    setMaxPrice(params.get('maxPrice') ? Number(params.get('maxPrice')) : undefined);
  }, [params]);

  useEffect(() => {
    let active = true;
    setBusy(true);
    setError('');

    tenantApi.properties({
      keyword: params.get('keyword') || undefined,
      province: params.get('province') || undefined,
      minPrice: params.get('minPrice') || undefined,
      maxPrice: params.get('maxPrice') || undefined,
      page,
      pageSize: PAGE_SIZE
    })
      .then(response => {
        if (active) {
          setMeta(response.data);
          setItems(response.data.items || []);
        }
      })
      .catch(error => {
        if (active) setError(errorText(error));
      })
      .finally(() => {
        if (active) setBusy(false);
      });

    return () => { active = false; };
  }, [params, page]);

  const submit = () => {
    if (minPrice !== undefined && maxPrice !== undefined && minPrice > maxPrice) {
      message.warning('Giá thấp nhất không được lớn hơn giá cao nhất.');
      return;
    }
    const next = new URLSearchParams();
    if (keyword.trim()) next.set('keyword', keyword.trim());
    if (province.trim()) next.set('province', province.trim());
    if (minPrice !== undefined) next.set('minPrice', String(minPrice));
    if (maxPrice !== undefined) next.set('maxPrice', String(maxPrice));
    next.set('page', '1');
    setParams(next);
  };

  const clearFilters = () => {
    setKeyword('');
    setProvince('');
    setMinPrice(undefined);
    setMaxPrice(undefined);
    setParams({});
  };

  return (
    <main className="page property-list-page">
      <div className="page-heading">
        <div>
          <span className="section-kicker">DANH SÁCH BẤT ĐỘNG SẢN</span>
          <h1>Tìm nhà cho thuê</h1>
          <p className="muted">Lọc theo từ khóa, khu vực và khoảng giá phù hợp với bạn.</p>
        </div>
        {meta && <Tag color="success">{meta.totalItems ?? meta.totalCount ?? 0} tin đăng</Tag>}
      </div>

      <Card className="filter-card">
        <div className="filter-grid">
          <Input
            value={keyword}
            onChange={event => setKeyword(event.target.value)}
            onPressEnter={submit}
            prefix={<SearchOutlined />}
            placeholder="Tên nhà, địa chỉ..."
            aria-label="Từ khóa tìm nhà"
          />
          <Input
            value={province}
            onChange={event => setProvince(event.target.value)}
            onPressEnter={submit}
            prefix={<EnvironmentOutlined />}
            placeholder="Tỉnh / thành phố"
            aria-label="Tỉnh thành phố"
          />
          <InputNumber
            value={minPrice}
            min={0}
            onChange={value => setMinPrice(value ?? undefined)}
            placeholder="Giá từ"
            addonAfter="đ"
            style={{ width: '100%' }}
          />
          <InputNumber
            value={maxPrice}
            min={0}
            onChange={value => setMaxPrice(value ?? undefined)}
            placeholder="Giá đến"
            addonAfter="đ"
            style={{ width: '100%' }}
          />
          <Space className="filter-actions">
            <Button type="primary" icon={<SearchOutlined />} onClick={submit}>Tìm kiếm</Button>
            <Button onClick={clearFilters}>Xóa lọc</Button>
          </Space>
        </div>
      </Card>

      {error && <Alert className="page-alert" type="error" showIcon message={error} />}
      {busy ? (
        <div className="home-loading"><Spin size="large" /></div>
      ) : items.length === 0 ? (
        <div className="home-empty">
          <Empty description="Không tìm thấy căn nhà phù hợp với bộ lọc hiện tại.">
            <Button onClick={clearFilters}>Xóa bộ lọc</Button>
          </Empty>
        </div>
      ) : (
        <Row gutter={[22, 22]} className="property-grid">
          {items.map(item => (
            <Col xs={24} sm={12} lg={8} key={item.id}>
              <PropertyCard item={item} to={'/nha/' + item.id + '?' + params.toString()} />
            </Col>
          ))}
        </Row>
      )}

      <PaginationBar
        page={page}
        pageSize={PAGE_SIZE}
        total={meta?.totalItems ?? meta?.totalCount ?? 0}
        onChange={value => {
          const next = new URLSearchParams(params);
          next.set('page', String(value));
          setParams(next);
        }}
      />
    </main>
  );
}

export function PropertyDetailPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const { user } = useAuth();
  const [property, setProperty] = useState<PropertyItem | null>(null);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState('');
  const [favorite, setFavorite] = useState(false);
  const [favoriteBusy, setFavoriteBusy] = useState(false);
  const [bookingOpen, setBookingOpen] = useState(false);
  const [bookingForm] = Form.useForm();

  useEffect(() => {
    let active = true;
    setBusy(true);
    setError('');
    setProperty(null);

    tenantApi.property(id)
      .then(response => { if (active) setProperty(response.data); })
      .catch(error => { if (active) setError(errorText(error)); })
      .finally(() => { if (active) setBusy(false); });

    return () => { active = false; };
  }, [id]);

  useEffect(() => {
    if (user && id && user.roles.includes('Tenant')) {
      tenantApi.favoriteStatus(id)
        .then(response => setFavorite(response.data.isFavorite))
        .catch(() => setFavorite(false));
    } else {
      setFavorite(false);
    }
  }, [user?.userId, id, user?.roles.join('|')]);

  const toggleFavorite = async () => {
    if (!user) {
      navigate('/dang-nhap', { state: { from: location.pathname + location.search } });
      return;
    }
    if (!user.roles.includes('Tenant')) {
      message.info('Chức năng yêu thích dành cho tài khoản người thuê.');
      return;
    }

    setFavoriteBusy(true);
    try {
      if (favorite) {
        await tenantApi.removeFavorite(id);
        setFavorite(false);
        message.success('Đã bỏ khỏi danh sách yêu thích.');
      } else {
        await tenantApi.addFavorite(id);
        setFavorite(true);
        message.success('Đã lưu vào danh sách yêu thích.');
      }
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setFavoriteBusy(false);
    }
  };

  const submitBooking = async (values: {
    startDate: string;
    endDate: string;
    numberOfPeople: number;
    proposedPrice: number;
    message?: string;
  }) => {
    try {
      await tenantApi.createBooking({ propertyId: id, ...values });
      message.success('Đã gửi yêu cầu thuê.');
      setBookingOpen(false);
      bookingForm.resetFields();
    } catch (error) {
      message.error(errorText(error));
    }
  };

  return (
    <main className="page property-detail-page">
      <Button type="link" className="back-link" onClick={() => navigate('/nha' + (location.search || ''))}>
        ← Quay lại danh sách
      </Button>

      {busy ? (
        <div className="home-loading"><Spin size="large" /></div>
      ) : error ? (
        <Alert type="error" showIcon message={error} />
      ) : !property ? (
        <Empty description="Không tìm thấy căn nhà." />
      ) : (
        <div className="property-detail-layout">
          <section className="property-detail-main">
            <div className="property-detail-heading">
              <div>
                <Tag color="success">{property.propertyTypeName || 'Bất động sản'}</Tag>
                <h1>{property.title}</h1>
                <p><EnvironmentOutlined /> {[property.addressLine, property.wardName, property.provinceName].filter(Boolean).join(', ')}</p>
              </div>
            </div>

            <div className="property-gallery">
              {(property.images || []).length > 0 ? property.images.map(image => (
                <img key={image.id} src={image.imageUrl} alt={property.title} loading="lazy" />
              )) : (
                <div className="property-gallery-placeholder"><HomeOutlined /><span>Chưa có hình ảnh</span></div>
              )}
            </div>

            <Card className="detail-info-card">
              <h2>Thông tin căn nhà</h2>
              <div className="detail-facts">
                <div><b>{property.area} m²</b><span>Diện tích</span></div>
                <div><b>{property.bedrooms}</b><span>Phòng ngủ</span></div>
                <div><b>{property.bathrooms}</b><span>Phòng tắm</span></div>
              </div>
              <h3>Mô tả</h3>
              <p className="detail-description">{property.description || 'Chủ nhà chưa bổ sung mô tả cho căn nhà này.'}</p>
            </Card>
          </section>

          <aside className="property-booking-card">
            <Card>
              <span className="section-kicker">GIÁ THUÊ</span>
              <div className="detail-price">{(property.pricePerMonth || 0).toLocaleString('vi-VN')} đ <small>/ tháng</small></div>
              <p className="muted">Giá thuê do chủ nhà cung cấp.</p>
              <Space direction="vertical" size={10} className="detail-actions">
                <Button block onClick={() => void toggleFavorite()} loading={favoriteBusy}>
                  {favorite ? 'Bỏ yêu thích' : 'Lưu yêu thích'}
                </Button>
                {user?.roles.includes('Tenant') && (
                  <>
                    <Button block type="primary" onClick={() => {
                      bookingForm.setFieldsValue({
                        numberOfPeople: 1,
                        proposedPrice: property.pricePerMonth
                      });
                      setBookingOpen(true);
                    }}>
                      Gửi yêu cầu thuê
                    </Button>
                    <Button
                      block
                      onClick={() => tenantApi.startConversation(property.id)
                        .then(response => navigate('/tin-nhan', { state: { conversationId: response.data.conversationId } }))
                        .catch(error => message.error(errorText(error)))}
                    >
                      Liên hệ chủ nhà
                    </Button>
                  </>
                )}
                {!user && <Button block type="primary" onClick={() => navigate('/dang-nhap', { state: { from: location.pathname } })}>Đăng nhập để thuê</Button>}
              </Space>
            </Card>
          </aside>
        </div>
      )}

      <Modal
        title="Gửi yêu cầu thuê"
        open={bookingOpen}
        onCancel={() => setBookingOpen(false)}
        onOk={() => bookingForm.submit()}
        okText="Gửi yêu cầu"
        cancelText="Hủy"
        destroyOnClose
      >
        <Form form={bookingForm} layout="vertical" onFinish={submitBooking}>
          <Form.Item name="startDate" label="Ngày bắt đầu" rules={[{ required: true, message: 'Vui lòng chọn ngày bắt đầu.' }]}>
            <Input type="date" />
          </Form.Item>
          <Form.Item
            name="endDate"
            label="Ngày kết thúc"
            dependencies={['startDate']}
            rules={[
              { required: true, message: 'Vui lòng chọn ngày kết thúc.' },
              {
                validator: (_, value) => {
                  const start = bookingForm.getFieldValue('startDate');
                  return !start || !value || value > start
                    ? Promise.resolve()
                    : Promise.reject(new Error('Ngày kết thúc phải sau ngày bắt đầu.'));
                }
              }
            ]}
          >
            <Input type="date" />
          </Form.Item>
          <Form.Item name="numberOfPeople" label="Số người ở" rules={[{ required: true, message: 'Vui lòng nhập số người.' }]}>
            <InputNumber min={1} max={100} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="proposedPrice" label="Giá đề xuất/tháng" rules={[{ required: true, type: 'number', min: 1, message: 'Giá đề xuất phải lớn hơn 0.' }]}>
            <InputNumber min={1} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="message" label="Lời nhắn">
            <Input.TextArea maxLength={1000} showCount rows={3} />
          </Form.Item>
        </Form>
      </Modal>
    </main>
  );
}
