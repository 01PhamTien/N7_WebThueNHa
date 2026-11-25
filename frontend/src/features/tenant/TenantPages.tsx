import { useEffect, useState } from 'react';
import type { Dispatch, ReactNode, SetStateAction } from 'react';
import {
  App as AntApp,
  Alert,
  Button,
  Card,
  Descriptions,
  Empty,
  Input,
  List,
  Modal,
  Pagination,
  Rate,
  Space,
  Spin,
  Tag,
  Typography
} from 'antd';
import {
  BellOutlined,
  CalendarOutlined,
  CheckOutlined,
  CreditCardOutlined,
  EnvironmentOutlined,
  EyeOutlined,
  FileTextOutlined,
  HeartFilled,
  HomeOutlined
} from '@ant-design/icons';
import { Link } from 'react-router-dom';
import { Page } from '../../components/Shared';
import { tenantApi } from '../../services/tenant';
import { errorText } from '../../services/api';
import type {
  PageResult,
  FavoriteItem,
  BookingItem,
  ContractItem,
  ContractDetail,
  InvoiceItem,
  InvoiceDetail,
  NotificationItem
} from '../../types/api';

const PAGE_SIZE = 10;

const bookingLabels: Record<string, string> = {
  Pending: 'Chờ xử lý',
  Accepted: 'Đã chấp nhận',
  Rejected: 'Đã từ chối',
  Cancelled: 'Đã hủy',
  Expired: 'Hết hạn',
  Unknown: 'Chưa xác định'
};

const contractLabels: Record<string, string> = {
  Draft: 'Bản nháp',
  AwaitingSignature: 'Chờ ký',
  Active: 'Đang hiệu lực',
  Completed: 'Hoàn thành',
  Terminated: 'Đã chấm dứt',
  Cancelled: 'Đã hủy'
};

const invoiceLabels: Record<string, string> = {
  Draft: 'Bản nháp',
  Issued: 'Đã phát hành',
  PartiallyPaid: 'Đã thanh toán một phần',
  Paid: 'Đã thanh toán',
  Overdue: 'Quá hạn',
  Cancelled: 'Đã hủy'
};

const paymentLabels: Record<string, string> = {
  Pending: 'Chờ xác nhận',
  Succeeded: 'Thành công',
  Failed: 'Thất bại',
  Cancelled: 'Đã hủy',
  Refunded: 'Đã hoàn tiền'
};

const statusColors: Record<string, string> = {
  Pending: 'gold',
  Accepted: 'green',
  Rejected: 'red',
  Cancelled: 'default',
  Expired: 'orange',
  Draft: 'default',
  AwaitingSignature: 'blue',
  Active: 'green',
  Completed: 'green',
  Terminated: 'red',
  Issued: 'blue',
  PartiallyPaid: 'gold',
  Paid: 'green',
  Overdue: 'red',
  Succeeded: 'green',
  Failed: 'red',
  Refunded: 'purple'
};

const money = (value?: number | null) => (value ?? 0).toLocaleString('vi-VN') + ' đ';

const dateText = (value?: string | null) => {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString('vi-VN');
};

const dateTimeText = (value?: string | null) => {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString('vi-VN');
};

function StatusTag({ value, labels }: { value: string; labels: Record<string, string> }) {
  return <Tag color={statusColors[value] || 'default'}>{labels[value] || value}</Tag>;
}

type TenantPageState<T> = {
  page: number;
  setPage: Dispatch<SetStateAction<number>>;
  data: PageResult<T> | null;
  busy: boolean;
  err: string;
  reload: () => void;
  setData: Dispatch<SetStateAction<PageResult<T> | null>>;
};

function usePage<T>(load: (page: number, size: number) => Promise<{ data: PageResult<T> }>): TenantPageState<T> {
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PageResult<T> | null>(null);
  const [busy, setBusy] = useState(true);
  const [err, setErr] = useState('');
  const [reloadKey, setReloadKey] = useState(0);

  const reload = () => setReloadKey(value => value + 1);

  useEffect(() => {
    let active = true;
    setBusy(true);
    setErr('');

    load(page, PAGE_SIZE)
      .then(response => {
        if (!active) return;
        if (!response?.data || !Array.isArray(response.data.items)) {
          throw new Error('Dữ liệu trả về từ máy chủ không đúng định dạng. Vui lòng thử lại.');
        }
        setData(response.data);
      })
      .catch(error => {
        if (!active) return;
        setData(null);
        setErr(errorText(error));
      })
      .finally(() => {
        if (active) setBusy(false);
      });

    return () => {
      active = false;
    };
  }, [load, page, reloadKey]);

  return { page, setPage, data, busy, err, reload, setData };
}

function PageFooter<T>({ state }: { state: TenantPageState<T> }) {
  const total = state.data?.totalItems ?? state.data?.totalCount ?? 0;

  if (total <= PAGE_SIZE) return null;

  return (
    <div className="tenant-pagination">
      <Pagination
        current={state.page}
        pageSize={PAGE_SIZE}
        total={total}
        showSizeChanger={false}
        showTotal={(count, range) => `${range[0]}–${range[1]} / ${count}`}
        onChange={state.setPage}
      />
    </div>
  );
}

function State<T>({
  state,
  children,
  empty
}: {
  state: TenantPageState<T>;
  children: (items: T[]) => ReactNode;
  empty: string;
}) {
  if (state.busy) {
    return (
      <Card className="tenant-state-card" bordered={false}>
        <div className="tenant-loading">
          <Spin size="large" />
          <Typography.Text type="secondary">Đang tải dữ liệu...</Typography.Text>
        </div>
      </Card>
    );
  }

  if (state.err) {
    return (
      <Alert
        className="tenant-load-error"
        type="error"
        showIcon
        message="Không thể tải dữ liệu"
        description={state.err}
        action={<Button size="small" onClick={state.reload}>Thử lại</Button>}
      />
    );
  }

  const items = Array.isArray(state.data?.items) ? state.data.items : [];

  if (items.length === 0) {
    return (
      <Card className="tenant-state-card tenant-empty-card" bordered={false}>
        <Empty description={empty} />
      </Card>
    );
  }

  return <>{children(items)}</>;
}

export function FavoritesPage() {
  const { message } = AntApp.useApp();
  const state = usePage<FavoriteItem>(tenantApi.favorites);
  const [removingId, setRemovingId] = useState<string | null>(null);

  const removeFavorite = async (propertyId: string) => {
    setRemovingId(propertyId);
    try {
      await tenantApi.removeFavorite(propertyId);
      message.success('Đã bỏ căn nhà khỏi danh sách yêu thích.');
      state.reload();
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setRemovingId(null);
    }
  };

  return (
    <Page title="Nhà yêu thích" description="Những căn nhà bạn đã lưu để xem lại và cân nhắc thuê.">
      <State state={state} empty="Bạn chưa lưu căn nhà nào." >
        {items => (
          <Card className="tenant-list-card" bordered={false}>
            <List
              dataSource={items}
              renderItem={item => (
                <List.Item
                  className="tenant-list-item"
                  actions={[
                    <Button
                      key="remove"
                      danger
                      type="text"
                      icon={<HeartFilled />}
                      loading={removingId === item.propertyId}
                      onClick={() => void removeFavorite(item.propertyId)}
                    >
                      Bỏ yêu thích
                    </Button>
                  ]}
                >
                  <List.Item.Meta
                    title={<Link to={'/nha/' + item.propertyId}>{item.title}</Link>}
                    description={
                      <Space className="tenant-item-meta" wrap size={[12, 6]}>
                        <span><EnvironmentOutlined /> {[item.addressLine, item.wardName, item.provinceName].filter(Boolean).join(', ')}</span>
                        <span>{item.area} m²</span>
                        <span>{item.bedrooms} phòng ngủ</span>
                      </Space>
                    }
                  />
                  <Typography.Text className="tenant-item-price" strong>{money(item.pricePerMonth)}<small> / tháng</small></Typography.Text>
                </List.Item>
              )}
            />
          </Card>
        )}
      </State>
      <PageFooter state={state} />
    </Page>
  );
}

export function BookingsPage() {
  const { message, modal } = AntApp.useApp();
  const state = usePage<BookingItem>(tenantApi.bookings);
  const [cancellingId, setCancellingId] = useState<string | null>(null);
  const [detail, setDetail] = useState<BookingItem | null>(null);
  const [loadingDetailId, setLoadingDetailId] = useState<string | null>(null);

  const openDetail = async (id: string) => {
    setLoadingDetailId(id);
    try {
      const response = await tenantApi.booking(id);
      setDetail(response.data);
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setLoadingDetailId(null);
    }
  };

  const cancelBooking = async (id: string) => {
    setCancellingId(id);
    try {
      const response = await tenantApi.cancelBooking(id);
      setDetail(current => current?.id === id ? response.data : current);
      message.success('Đã hủy yêu cầu thuê. Chủ nhà cũng sẽ nhận được thông báo.');
      state.reload();
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setCancellingId(null);
    }
  };

  const confirmCancel = (id: string) => modal.confirm({
    title: 'Hủy yêu cầu thuê này?',
    content: 'Yêu cầu sẽ chuyển sang trạng thái Đã hủy và chủ nhà sẽ được thông báo.',
    okText: 'Hủy yêu cầu',
    cancelText: 'Giữ yêu cầu',
    okButtonProps: { danger: true, loading: cancellingId === id },
    onOk: () => cancelBooking(id)
  });

  const nextStep = (status: string) => {
    if (status === 'Pending') return 'Yêu cầu đã được gửi. Chủ nhà sẽ xem thông tin và phản hồi; bạn có thể hủy khi yêu cầu còn đang chờ.';
    if (status === 'Accepted') return 'Chủ nhà đã đồng ý cho thuê. Bước tiếp theo là chủ nhà lập hợp đồng, bổ sung tiền cọc và điều khoản rồi gửi cho bạn xác nhận. Hãy theo dõi mục Hợp đồng thuê.';
    if (status === 'Rejected') return 'Yêu cầu không được chấp nhận. Bạn có thể xem lý do bên dưới hoặc tìm căn nhà khác.';
    if (status === 'Cancelled') return 'Yêu cầu này đã được hủy. Nếu vẫn muốn thuê căn nhà, bạn có thể liên hệ chủ nhà hoặc gửi yêu cầu mới khi tin đăng còn mở.';
    if (status === 'Expired') return 'Yêu cầu đã hết hạn. Vui lòng liên hệ chủ nhà nếu bạn vẫn quan tâm căn nhà.';
    return 'Chưa xác định được trạng thái yêu cầu. Vui lòng tải lại hoặc liên hệ hỗ trợ.';
  };

  return (
    <Page title="Yêu cầu thuê của tôi" description="Theo dõi từng yêu cầu, xem lại thông tin đã gửi và biết bước tiếp theo sau khi chủ nhà phản hồi.">
      <State state={state} empty="Bạn chưa gửi yêu cầu thuê nào.">
        {items => (
          <Card className="tenant-list-card" bordered={false}>
            <List
              dataSource={items}
              renderItem={item => (
                <List.Item
                  className="tenant-list-item"
                  actions={[
                    <Button
                      key="detail"
                      type="default"
                      icon={<EyeOutlined />}
                      loading={loadingDetailId === item.id}
                      onClick={() => void openDetail(item.id)}
                    >
                      Chi tiết yêu cầu
                    </Button>,
                    ...(item.status === 'Pending' ? [
                      <Button
                        key="cancel"
                        danger
                        type="text"
                        loading={cancellingId === item.id}
                        onClick={() => confirmCancel(item.id)}
                      >
                        Hủy yêu cầu
                      </Button>
                    ] : [])
                  ]}
                >
                  <List.Item.Meta
                    title={<span className="tenant-booking-title">{item.propertyTitle}</span>}
                    description={
                      <Space className="tenant-item-meta" wrap size={[12, 6]}>
                        <span><CalendarOutlined /> {dateText(item.startDate)} – {dateText(item.endDate)}</span>
                        <span>{item.numberOfPeople} người</span>
                        <span>{money(item.proposedPrice)}/tháng</span>
                        {item.estimatedRent !== undefined && <span>Ước tính {money(item.estimatedRent)}</span>}
                        <Link to={'/nha/' + item.propertyId}>Xem tin nhà</Link>
                      </Space>
                    }
                  />
                  <StatusTag value={item.status} labels={bookingLabels} />
                </List.Item>
              )}
            />
          </Card>
        )}
      </State>
      <PageFooter state={state} />

      <Modal
        title="Chi tiết yêu cầu thuê"
        open={!!detail}
        onCancel={() => setDetail(null)}
        footer={[
          <Button key="close" onClick={() => setDetail(null)}>Đóng</Button>,
          ...(detail?.status === 'Pending' ? [
            <Button key="cancel" danger loading={cancellingId === detail.id} onClick={() => confirmCancel(detail.id)}>Hủy yêu cầu</Button>
          ] : []),
          ...(detail?.status === 'Accepted' ? [
            <Button key="contract" type="primary" onClick={() => { setDetail(null); window.location.assign('/hop-dong'); }}>Theo dõi hợp đồng</Button>
          ] : [])
        ]}
        width="min(680px, calc(100vw - 32px))"
        destroyOnHidden
      >
        {detail && (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Space wrap align="center">
              <StatusTag value={detail.status} labels={bookingLabels} />
              <Typography.Text type="secondary">Gửi lúc {dateTimeText(detail.createdAt)}</Typography.Text>
            </Space>
            <Alert
              type={detail.status === 'Rejected' ? 'warning' : detail.status === 'Accepted' ? 'success' : detail.status === 'Cancelled' || detail.status === 'Expired' ? 'info' : 'info'}
              showIcon
              message={detail.status === 'Pending' ? 'Đang chờ chủ nhà phản hồi' : detail.status === 'Accepted' ? 'Chủ nhà đã xác nhận yêu cầu' : detail.status === 'Rejected' ? 'Yêu cầu không được chấp nhận' : bookingLabels[detail.status] || detail.status}
              description={nextStep(detail.status)}
            />
            <Descriptions bordered column={1} size="small">
              <Descriptions.Item label="Căn nhà">
                <Space direction="vertical" size={2}>
                  <Typography.Text strong>{detail.propertyTitle}</Typography.Text>
                  <Link to={'/nha/' + detail.propertyId} onClick={() => setDetail(null)}>Xem thông tin căn nhà</Link>
                </Space>
              </Descriptions.Item>
              <Descriptions.Item label="Thời gian thuê">Từ {dateText(detail.startDate)} đến trước {dateText(detail.endDate)}</Descriptions.Item>
              <Descriptions.Item label="Số người thuê">{detail.numberOfPeople} người</Descriptions.Item>
              <Descriptions.Item label="Giá đề xuất">{money(detail.proposedPrice)}/tháng</Descriptions.Item>
              <Descriptions.Item label="Ước tính tiền thuê cả kỳ">{money(detail.estimatedRent)}</Descriptions.Item>
              <Descriptions.Item label="Lời nhắn">{detail.message || 'Không có lời nhắn'}</Descriptions.Item>
              <Descriptions.Item label="Thời điểm chủ nhà phản hồi">{dateTimeText(detail.respondedAt)}</Descriptions.Item>
              {detail.status === 'Rejected' && <Descriptions.Item label="Lý do từ chối">{detail.rejectionReason || 'Chủ nhà chưa cung cấp lý do.'}</Descriptions.Item>}
              {detail.status === 'Accepted' && <Descriptions.Item label="Bước tiếp theo">Chờ chủ nhà lập và gửi hợp đồng để bạn xem, ký xác nhận.</Descriptions.Item>}
            </Descriptions>
            {detail.status === 'Accepted' && (
              <Alert type="info" showIcon message="Hợp đồng và thanh toán là bước riêng" description="Việc chủ nhà chấp nhận yêu cầu chưa có nghĩa là hợp đồng đã được ký hoặc tiền cọc đã được thanh toán. Chỉ thanh toán theo hóa đơn/hợp đồng hiển thị trong tài khoản." />
            )}
          </Space>
        )}
      </Modal>
    </Page>
  );
}

export function ContractsPage() {
  const { message } = AntApp.useApp();
  const state = usePage<ContractItem>(tenantApi.contracts);
  const [detail, setDetail] = useState<ContractDetail | null>(null);
  const [loadingDetailId, setLoadingDetailId] = useState<string | null>(null);
  const [signingId, setSigningId] = useState<string | null>(null);
  const [reviewContract, setReviewContract] = useState<ContractItem | null>(null);
  const [reviewRating, setReviewRating] = useState(5);
  const [reviewComment, setReviewComment] = useState('');
  const [reviewSubmitting, setReviewSubmitting] = useState(false);
  const [reviewedContractIds, setReviewedContractIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    tenantApi.myReviews()
      .then(response => setReviewedContractIds(new Set(response.data.map(review => review.contractId))))
      .catch(() => setReviewedContractIds(new Set()));
  }, []);

  const canReview = (item: ContractItem) => {
    if (item.status === 'Completed') return true;
    if (item.status !== 'Active') return false;
    return item.endDate <= new Date(Date.now() + 7 * 60 * 60 * 1000).toISOString().slice(0, 10);
  };

  const submitReview = async () => {
    if (!reviewContract) return;
    setReviewSubmitting(true);
    try {
      await tenantApi.createReview(reviewContract.id, {
        rating: reviewRating,
        comment: reviewComment.trim() || undefined
      });
      setReviewedContractIds(previous => new Set(previous).add(reviewContract.id));
      message.success('Cảm ơn bạn đã chia sẻ trải nghiệm thuê nhà.');
      setReviewContract(null);
      setReviewComment('');
      setReviewRating(5);
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setReviewSubmitting(false);
    }
  };

  const openDetail = async (id: string) => {
    setLoadingDetailId(id);
    try {
      const response = await tenantApi.contract(id);
      setDetail(response.data);
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setLoadingDetailId(null);
    }
  };

  const signContract = async (id: string) => {
    setSigningId(id);
    try {
      await tenantApi.signContract(id);
      message.success('Đã xác nhận chữ ký hợp đồng.');
      state.reload();
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setSigningId(null);
    }
  };

  return (
    <Page title="Hợp đồng thuê" description="Xem thời hạn, điều khoản và trạng thái các hợp đồng thuê của bạn.">
      <State state={state} empty="Chưa có hợp đồng thuê.">
        {items => (
          <Card className="tenant-list-card" bordered={false}>
            <List
              dataSource={items}
              renderItem={item => (
                <List.Item
                  className="tenant-list-item"
                  actions={[
                    <Button
                      key="detail"
                      type="default"
                      icon={<EyeOutlined />}
                      loading={loadingDetailId === item.id}
                      onClick={() => void openDetail(item.id)}
                    >
                      Chi tiết
                    </Button>,
                    ...(item.status === 'AwaitingSignature' ? [
                      <Button
                        key="sign"
                        type="primary"
                        loading={signingId === item.id}
                        onClick={() => void signContract(item.id)}
                      >
                        Ký hợp đồng
                      </Button>
                    ] : []),
                    ...(canReview(item) && !reviewedContractIds.has(item.id) ? [
                      <Button
                        key="review"
                        onClick={() => { setReviewRating(5); setReviewComment(''); setReviewContract(item); }}
                      >
                        Đánh giá
                      </Button>
                    ] : [])
                  ]}
                >
                  <List.Item.Meta
                    title={<span className="tenant-code">{item.contractCode}</span>}
                    description={
                      <Space className="tenant-item-meta" wrap size={[12, 6]}>
                        <span><HomeOutlined /> {item.propertyTitle}</span>
                        <span><CalendarOutlined /> {dateText(item.startDate)} – {dateText(item.endDate)}</span>
                        <span>{money(item.monthlyRent)}/tháng</span>
                        {item.estimatedRent !== undefined && <span>Ước tính cả kỳ: {money(item.estimatedRent)}</span>}
                      </Space>
                    }
                  />
                  <StatusTag value={item.status} labels={contractLabels} />
                </List.Item>
              )}
            />
          </Card>
        )}
      </State>
      <PageFooter state={state} />

      <Modal
        title="Chi tiết hợp đồng"
        open={!!detail}
        onCancel={() => setDetail(null)}
        footer={null}
        width="min(760px, calc(100vw - 32px))"
        destroyOnHidden
      >
        {detail && (
          <Descriptions bordered column={1} size="small">
            <Descriptions.Item label="Mã hợp đồng">{detail.contract.contractCode}</Descriptions.Item>
            <Descriptions.Item label="Căn nhà">{detail.propertyTitle}</Descriptions.Item>
            <Descriptions.Item label="Chủ nhà">{detail.landlordName}</Descriptions.Item>
            <Descriptions.Item label="Người thuê">{detail.tenantName}</Descriptions.Item>
            <Descriptions.Item label="Thời hạn">Từ {dateText(detail.contract.startDate)} đến trước {dateText(detail.contract.endDate)}</Descriptions.Item>
            <Descriptions.Item label="Tiền thuê">{money(detail.contract.monthlyRent)}/tháng</Descriptions.Item>
            <Descriptions.Item label="Tổng tiền thuê ước tính">{money(detail.contract.estimatedRent)}</Descriptions.Item>
            <Descriptions.Item label="Tiền cọc">{money(detail.contract.depositAmount)}</Descriptions.Item>
            <Descriptions.Item label="Ngày đến hạn hàng tháng">Ngày {detail.contract.paymentDueDay}</Descriptions.Item>
            <Descriptions.Item label="Trạng thái"><StatusTag value={detail.contract.status} labels={contractLabels} /></Descriptions.Item>
            <Descriptions.Item label="Điều khoản">{detail.contract.terms || 'Chưa có điều khoản bổ sung.'}</Descriptions.Item>
          </Descriptions>
        )}
      </Modal>

      <Modal
        title="Đánh giá căn nhà"
        open={!!reviewContract}
        onCancel={() => setReviewContract(null)}
        onOk={() => void submitReview()}
        okText="Gửi đánh giá"
        cancelText="Để sau"
        confirmLoading={reviewSubmitting}
        destroyOnHidden
      >
        {reviewContract && (
          <Space orientation="vertical" size={12} style={{ width: '100%' }}>
            <Typography.Text strong>{reviewContract.propertyTitle}</Typography.Text>
            <Rate allowClear={false} value={reviewRating} onChange={setReviewRating} />
            <Input.TextArea
              value={reviewComment}
              onChange={event => setReviewComment(event.target.value)}
              maxLength={2000}
              showCount
              rows={4}
              placeholder="Chia sẻ ngắn gọn về căn nhà và trải nghiệm thuê (không bắt buộc)."
            />
          </Space>
        )}
      </Modal>
    </Page>
  );
}


export function InvoicesPage() {
  const { message } = AntApp.useApp();
  const state = usePage<InvoiceItem>(tenantApi.invoices);
  const [detail, setDetail] = useState<InvoiceDetail | null>(null);
  const [loadingDetailId, setLoadingDetailId] = useState<string | null>(null);
  const [checkoutId, setCheckoutId] = useState<string | null>(null);

  const openDetail = async (id: string) => {
    setLoadingDetailId(id);
    try {
      const response = await tenantApi.invoice(id);
      setDetail(response.data);
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setLoadingDetailId(null);
    }
  };

  const checkout = async (id: string) => {
    setCheckoutId(id);
    try {
      const response = await tenantApi.checkout(id);
      if (response.data.checkoutUrl) {
        window.location.assign(response.data.checkoutUrl);
      } else {
        message.info('Chưa nhận được đường dẫn thanh toán từ máy chủ.');
      }
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setCheckoutId(null);
    }
  };

  return (
    <Page title="Hóa đơn và thanh toán" description="Tra cứu các khoản cần thanh toán và lịch sử giao dịch của bạn.">
      <State state={state} empty="Bạn chưa có hóa đơn nào.">
        {items => (
          <Card className="tenant-list-card" bordered={false}>
            <List
              dataSource={items}
              renderItem={item => (
                <List.Item
                  className="tenant-list-item"
                  actions={[
                    <Button
                      key="detail"
                      icon={<EyeOutlined />}
                      loading={loadingDetailId === item.id}
                      onClick={() => void openDetail(item.id)}
                    >
                      Chi tiết
                    </Button>,
                    ...(item.status !== 'Paid' && item.status !== 'Cancelled' ? [
                      <Button
                        key="pay"
                        type="primary"
                        icon={<CreditCardOutlined />}
                        loading={checkoutId === item.id}
                        onClick={() => void checkout(item.id)}
                      >
                        Thanh toán
                      </Button>
                    ] : [])
                  ]}
                >
                  <List.Item.Meta
                    title={<span className="tenant-code">{item.invoiceCode}</span>}
                    description={
                      <Space className="tenant-item-meta" wrap size={[12, 6]}>
                        <span><FileTextOutlined /> {item.propertyTitle}</span>
                        <span>Hạn thanh toán: {dateText(item.dueDate)}</span>
                      </Space>
                    }
                  />
                  <div className="tenant-amount-block">
                    <Typography.Text className="tenant-item-price" strong>{money(item.totalAmount)}</Typography.Text>
                    <StatusTag value={item.status} labels={invoiceLabels} />
                  </div>
                </List.Item>
              )}
            />
          </Card>
        )}
      </State>
      <PageFooter state={state} />

      <Modal
        title="Chi tiết hóa đơn và lịch sử thanh toán"
        open={!!detail}
        onCancel={() => setDetail(null)}
        footer={null}
        width="min(800px, calc(100vw - 32px))"
        destroyOnHidden
      >
        {detail && (
          <div className="tenant-invoice-detail">
            <Descriptions bordered column={2} size="small">
              <Descriptions.Item label="Mã hóa đơn">{detail.invoiceCode}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái"><StatusTag value={detail.status} labels={invoiceLabels} /></Descriptions.Item>
              <Descriptions.Item label="Tổng tiền">{money(detail.totalAmount)}</Descriptions.Item>
              <Descriptions.Item label="Hạn thanh toán">{dateText(detail.dueDate)}</Descriptions.Item>
              <Descriptions.Item label="Căn nhà" span={2}>{detail.propertyTitle}</Descriptions.Item>
              <Descriptions.Item label="Kỳ thanh toán" span={2}>
                {detail.periodStart ? dateText(detail.periodStart) + ' – ' + dateText(detail.periodEnd) : '—'}
              </Descriptions.Item>
            </Descriptions>

            <Typography.Title level={5}>Các khoản phí</Typography.Title>
            <List
              dataSource={detail.items ?? []}
              renderItem={item => (
                <List.Item>
                  <List.Item.Meta title={item.description} description={item.quantity + ' × ' + money(item.unitPrice)} />
                  <Typography.Text strong>{money(item.amount)}</Typography.Text>
                </List.Item>
              )}
            />

            <Typography.Title level={5}>Lịch sử giao dịch</Typography.Title>
            <List
              dataSource={detail.payments ?? []}
              locale={{ emptyText: <Empty description="Chưa có giao dịch." /> }}
              renderItem={payment => (
                <List.Item>
                  <List.Item.Meta
                    title={payment.paymentMethod + ' · ' + (paymentLabels[payment.status] || payment.status)}
                    description={payment.transactionCode || payment.paymentProvider || 'Không có mã giao dịch'}
                  />
                  <Space orientation="vertical" align="end" size={2}>
                    <Typography.Text strong>{money(payment.amount)}</Typography.Text>
                    <Typography.Text type="secondary">{dateTimeText(payment.paidAt || payment.createdAt)}</Typography.Text>
                  </Space>
                </List.Item>
              )}
            />
          </div>
        )}
      </Modal>
    </Page>
  );
}

export function NotificationsPage() {
  const { message } = AntApp.useApp();
  const state = usePage<NotificationItem>(tenantApi.notifications);
  const [markingAll, setMarkingAll] = useState(false);
  const [markingId, setMarkingId] = useState<string | null>(null);
  const items = state.data?.items ?? [];
  const unreadCount = items.filter(item => !item.isRead).length;

  const markAllRead = async () => {
    setMarkingAll(true);
    try {
      await tenantApi.readAll();
      window.dispatchEvent(new Event('web2:notifications-updated'));
      state.setData(previous => previous
        ? { ...previous, items: previous.items.map(item => ({ ...item, isRead: true })) }
        : previous);
      message.success('Đã đánh dấu tất cả thông báo là đã đọc.');
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setMarkingAll(false);
    }
  };

  const markRead = async (id: string) => {
    setMarkingId(id);
    try {
      await tenantApi.readNotification(id);
      window.dispatchEvent(new Event('web2:notifications-updated'));
      state.setData(previous => previous
        ? { ...previous, items: previous.items.map(item => item.id === id ? { ...item, isRead: true } : item) }
        : previous);
      message.success('Đã đánh dấu thông báo là đã đọc.');
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setMarkingId(null);
    }
  };

  return (
    <Page title="Thông báo" description="Cập nhật mới nhất về yêu cầu thuê, hợp đồng và thanh toán của bạn.">
      <div className="tenant-page-toolbar">
        <div className="tenant-unread-summary">
          <span className="tenant-unread-icon"><BellOutlined /></span>
          <div>
            <Typography.Text strong>{unreadCount} thông báo chưa đọc</Typography.Text>
            <Typography.Text type="secondary">Trong danh sách hiện tại</Typography.Text>
          </div>
        </div>
        <Button
          icon={<CheckOutlined />}
          disabled={state.busy || !!state.err}
          loading={markingAll}
          onClick={() => void markAllRead()}
        >
          Đánh dấu tất cả đã đọc
        </Button>
      </div>

      <State state={state} empty="Bạn chưa có thông báo nào.">
        {notifications => (
          <Card className="tenant-list-card tenant-notification-card" bordered={false}>
            <List
              dataSource={notifications}
              renderItem={notification => (
                <List.Item className={'tenant-list-item tenant-notification-item' + (!notification.isRead ? ' is-unread' : '')}>
                  <List.Item.Meta
                    title={
                      <Space wrap size={8}>
                        <span className="tenant-notification-title">{notification.title}</span>
                        {!notification.isRead && <Tag color="blue">Mới</Tag>}
                      </Space>
                    }
                    description={
                      <div className="tenant-notification-content">
                        <Typography.Paragraph ellipsis={{ rows: 3, expandable: true, symbol: 'Xem thêm' }}>
                          {notification.content}
                        </Typography.Paragraph>
                        <Typography.Text type="secondary">{dateTimeText(notification.createdAt)}</Typography.Text>
                      </div>
                    }
                  />
                  {!notification.isRead && (
                    <Button
                      type="text"
                      icon={<CheckOutlined />}
                      loading={markingId === notification.id}
                      onClick={() => void markRead(notification.id)}
                    >
                      Đánh dấu đã đọc
                    </Button>
                  )}
                </List.Item>
              )}
            />
          </Card>
        )}
      </State>
      <PageFooter state={state} />
    </Page>
  );
}
