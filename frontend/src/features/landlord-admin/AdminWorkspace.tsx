import { useEffect, useState } from 'react';
import {
  Alert,
  Progress,
  Segmented,
  App as AntApp,
  Avatar,
  Button,
  Card,
  Col,
  Descriptions,
  Drawer,
  Form,
  Input,
  List,
  Modal,
  Popconfirm,
  Row,
  Select,
  Space,
  Statistic,
  Table,
  Tabs,
  Tag,
  Typography
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useSearchParams } from 'react-router-dom';
import { errorText } from '../../services/api';
import { landlordAdminApi as api } from '../../services/landlordAdmin';
import type {
  AdminUserDto,
  AdminMoveOutDto,
  AdminPaymentListDto,
  AdminReviewDto,
  AdminModerationLogDto,
  PagedResult,
  CreateAdminUserRequest,
  ModerationPropertyDto,
  PropertyImageDto,
  UpdateAdminUserRequest
} from '../../services/landlordAdmin';
import { Page, Loading, EmptyState } from '../../components/Shared';
import {
  bookingStatusLabels,
  contractStatusLabels,
  invoiceStatusLabels,
  paymentStatusLabels,
  dateText,
  listingStatusLabels,
  moderationActionLabels,
  moderationStatusLabels,
  money,
  Status,
  ErrorLine,
  Paging,
  useRequest
} from './workspaceShared';

interface AdminUserFormValues {
  fullName: string;
  email?: string;
  password?: string;
  phoneNumber?: string;
  avatarUrl?: string;
  roles: string[];
}

const adminRoleLabels:Record<string,string>={Admin:'Quản trị viên',Landlord:'Chủ nhà',Tenant:'Người thuê'};
const moveOutLabels:Record<string,string>={Pending:'Chờ duyệt',Approved:'Chờ bàn giao',InspectionScheduled:'Chờ bàn giao',AwaitingSettlement:'Chờ ký bàn giao',Completed:'Hoàn tất',Rejected:'Từ chối',Cancelled:'Đã hủy'};
export function AdminDashboard() {
 const q=useRequest(()=>api.adminDashboard(),[]);
 const [months,setMonths]=useState(6);
 const d=q.data;
 useEffect(()=>{const reload=()=>{if(document.visibilityState==='visible')q.refresh();};window.addEventListener('focus',reload);return()=>window.removeEventListener('focus',reload);},[q.refresh]);
 const kpis=[
 {title:'Người dùng',value:d?.totalUsers,note:d?d.activeUsers+' hoạt động · '+d.newUsersThisMonth+' mới tháng này':'—',href:'/admin/users'},
 {title:'Tin đăng',value:d?.totalProperties,note:d?d.publishedProperties+' đang công khai':'—',href:'/admin/moderation?status=all'},
 {title:'Hợp đồng hiệu lực',value:d?.activeContracts,note:d?d.totalContracts+' hợp đồng toàn hệ thống':'—',href:'/admin/operations?tab=contracts&status=Active'},
 {title:'Tiền thu tháng này',value:d?money(d.receivedThisMonth):undefined,note:'Giao dịch thành công, gồm tiền cọc',href:'/admin/operations?tab=payments&status=Succeeded'},
 {title:'Tiền hoàn tháng này',value:d?money(d.refundedThisMonth):undefined,note:'Quyết toán trả nhà hoàn tất',href:'/admin/operations?tab=move-outs&status=Completed'},
 {title:'Công nợ quá hạn',value:d?money(d.overdueAmount):undefined,note:d?d.overdueInvoices+' hóa đơn quá hạn':'—',href:'/admin/operations?tab=invoices&status=Overdue'}
 ];
 const tasks=[
 {label:'Tin đăng chờ kiểm duyệt',value:d?.pendingProperties,href:'/admin/moderation?status=Pending'},
 {label:'Yêu cầu thuê chờ xử lý',value:d?.pendingBookings,href:'/admin/operations?tab=bookings&status=Pending'},
 {label:'Hợp đồng chờ ký',value:d?.awaitingSignature,href:'/admin/operations?tab=contracts&status=AwaitingSignature'},
 {label:'Thanh toán chờ xác nhận',value:d?.pendingPayments,href:'/admin/operations?tab=payments&status=Pending'},
 {label:'Yêu cầu trả nhà mới',value:d?.pendingMoveOuts,href:'/admin/operations?tab=move-outs&status=Pending'},
 {label:'Biên bản trả nhà chờ ký',value:d?.awaitingSettlement,href:'/admin/operations?tab=move-outs&status=AwaitingSettlement'}
 ];
 const trend=(d?.trend||[]).slice(-months);
 const max=Math.max(1,...trend.flatMap(x=>[x.received,x.refunded]));
 return <Page title="Tổng quan quản trị" description="Theo dõi sức khỏe hệ thống, kiểm duyệt và toàn bộ vòng đời thuê nhà.">
 <div className="landlord-dashboard admin-dashboard"><ErrorLine error={q.error}/><div className="ld-toolbar"><Typography.Text type="secondary">Dữ liệu toàn hệ thống · giờ Việt Nam</Typography.Text><Button onClick={q.refresh} loading={q.busy}>Làm mới</Button></div>
 <div className="ld-kpis">{kpis.map(x=><Card key={x.title} size="small"><Statistic title={x.title} value={x.value??'—'} loading={q.busy}/><Typography.Link href={x.href}>{x.note}</Typography.Link></Card>)}</div>
 <div className="ld-main-grid"><Card title="Cần theo dõi" className="ld-panel"><div className="ld-tasks ld-single">{tasks.map(x=><a className="ld-task" href={x.href} key={x.label}><span>{x.label}</span><Tag color={x.value?'gold':'default'}>{x.value??'—'}</Tag></a>)}</div><Typography.Text type="secondary">Admin kiểm duyệt tin đăng; các bên liên quan xử lý nghiệp vụ thuê và trả nhà.</Typography.Text></Card>
 <Card title="Phân bổ tài khoản" className="ld-panel" extra={<Typography.Link href="/admin/users">Quản lý</Typography.Link>}>{['Tenant','Landlord','Admin'].map(role=>{const n=d?.roleCounts.find(x=>x.role===role)?.count;return <div className="ld-task" key={role}><span>{adminRoleLabels[role]}</span><strong>{d?n??0:'—'}</strong></div>;})}<Typography.Text type="secondary">Một tài khoản có thể có nhiều vai trò.</Typography.Text><div className="ld-due"><Statistic title="Tài khoản đã khóa" value={d?d.totalUsers-d.activeUsers:'—'}/></div></Card></div>
 <Card title="Dòng tiền qua hệ thống" className="ld-panel" extra={<Segmented value={months} onChange={v=>setMonths(Number(v))} options={[{label:'6 tháng',value:6},{label:'12 tháng',value:12}]}/>}>{q.busy?<Loading/>:!d?<EmptyState/>:<><div className="ld-totals"><Statistic title="Thu tháng này" value={money(d.receivedThisMonth)}/><Statistic title="Thu trừ hoàn tháng này" value={money(d.receivedThisMonth-d.refundedThisMonth)}/><Statistic title="Tổng công nợ còn lại" value={money(d.outstandingAmount)}/></div><div className="admin-trend">{trend.map(x=><div className="admin-trend-row" key={x.label}><span>{x.label}</span><div><Progress percent={x.received/max*100} showInfo={false} strokeColor="#24765f"/><Progress percent={x.refunded/max*100} showInfo={false} strokeColor="#d99446"/></div><div><strong>{money(x.received)}</strong><Typography.Text type="secondary">{money(x.refunded)} hoàn</Typography.Text></div></div>)}</div><Typography.Text type="secondary">Xanh: tiền thu thành công; cam: tiền hoàn khi trả nhà. Đây là dòng tiền của các bên, không phải doanh thu của nền tảng.</Typography.Text></>}</Card>
 <div className="ld-main-grid"><Card title="Hàng đợi kiểm duyệt" extra={<Typography.Link href="/admin/moderation">Xem tất cả</Typography.Link>}>{q.busy?<Loading/>:!d?.moderationQueue.length?<EmptyState description="Không có tin chờ duyệt"/>:d.moderationQueue.map(x=><div className="ld-record" key={x.id}><div className="ld-record-text"><Typography.Link href="/admin/moderation">{x.title}</Typography.Link><Typography.Text type="secondary">{x.landlordName} · {dateText(x.createdAt)}</Typography.Text></div><Tag color="gold">Chờ duyệt</Tag></div>)}</Card>
 <Card title="Vòng đời thuê nhà" extra={<Typography.Link href="/admin/operations">Giám sát</Typography.Link>}>{[{label:'Tổng yêu cầu thuê',v:d?.totalBookings},{label:'Hợp đồng sắp hết hạn (30 ngày)',v:d?.endingSoon},{label:'Trả nhà đang xử lý',v:d?.openMoveOutCount},{label:'Bàn giao đến hạn',v:d?.handoverDue},{label:'Trả nhà hoàn tất',v:d?.completedMoveOuts},{label:'Tổng đánh giá',v:d?.totalReviews},{label:'Lượt kiểm duyệt',v:d?.totalModerationActions}].map(x=><div key={x.label} className="ld-task"><span>{x.label}</span><strong>{x.v??'—'}</strong></div>)}</Card></div>
 </div></Page>;
}

const emptyAdminPage = <T,>(): { data: PagedResult<T> } => ({
  data: { items: [], page: 1, pageSize: 10, totalItems: 0 }
});

export function AdminOperations() {
  const [params,setParams]=useSearchParams();
  const allowedTabs=['bookings','contracts','invoices','payments','move-outs','reviews','activity'];
  const requestedTab=params.get('tab')||'bookings';
  const activeTab=allowedTabs.includes(requestedTab)?requestedTab:'bookings';
  const status=params.get('status')||undefined;
  const [record,setRecord]=useState<{title:string;fields:Array<{label:string;value:React.ReactNode}>}|null>(null);
  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState('');

  const bookings = useRequest(
    () => activeTab === 'bookings'
      ? api.adminBookings({ page, pageSize: 10, keyword: keyword.trim() || undefined, status })
      : Promise.resolve(emptyAdminPage<import('../../services/landlordAdmin').BookingDto>()),
    [activeTab, page, keyword,status]
  );
  const contracts = useRequest(
    () => activeTab === 'contracts'
      ? api.adminContracts({ page, pageSize: 10, keyword: keyword.trim() || undefined, status })
      : Promise.resolve(emptyAdminPage<import('../../services/landlordAdmin').ContractDto>()),
    [activeTab, page, keyword,status]
  );
  const invoices = useRequest(
    () => activeTab === 'invoices'
      ? api.adminInvoices({ page, pageSize: 10, keyword: keyword.trim() || undefined, status })
      : Promise.resolve(emptyAdminPage<import('../../services/landlordAdmin').InvoiceListDto>()),
    [activeTab, page, keyword,status]
  );
  const payments = useRequest(
    () => activeTab === 'payments'
      ? api.adminPayments({ page, pageSize: 10, keyword: keyword.trim() || undefined, status })
      : Promise.resolve(emptyAdminPage<AdminPaymentListDto>()),
    [activeTab, page, keyword,status]
  );
  const reviews = useRequest(
    () => activeTab === 'reviews'
      ? api.adminReviews({ page, pageSize: 10, keyword: keyword.trim() || undefined, rating:status?Number(status):undefined })
      : Promise.resolve(emptyAdminPage<AdminReviewDto>()),
    [activeTab, page, keyword,status]
  );
  const logs = useRequest(
    () => activeTab === 'activity'
      ? api.adminModerationLogs({ page, pageSize: 10, keyword: keyword.trim() || undefined, action:status })
      : Promise.resolve(emptyAdminPage<AdminModerationLogDto>()),
    [activeTab, page, keyword,status]
  );

  const moveOuts=useRequest(()=>activeTab==='move-outs'?api.adminMoveOuts({page,pageSize:10,keyword:keyword.trim()||undefined,status}):Promise.resolve(emptyAdminPage<AdminMoveOutDto>()),[activeTab,page,keyword,status]);
  useEffect(()=>{setPage(1);setKeyword('');},[activeTab]);
  const switchTab = (key: string) => {
    setParams({tab:key});
    setPage(1);
    setKeyword('');
  };

  const renderTable = <T extends { id: string }>(
    q: ReturnType<typeof useRequest<PagedResult<T>>>,
    columns: ColumnsType<T>,
    emptyText: string
  ) => (
    <Card className="management-table-card">
      <ErrorLine error={q.error} />
      {q.busy ? <Loading /> : (
        <Table
          rowKey="id"
          columns={[...columns,{title:'Chi tiết',key:'detail',render:(_,row)=><Button size="small" onClick={()=>setRecord({title:'Chi tiết hồ sơ',fields:columns.filter(c=>'dataIndex' in c&&typeof c.dataIndex==='string').map(c=>{const col=c as {title:string;dataIndex:string;render?:(v:unknown,r:T,i:number)=>React.ReactNode};const v=(row as unknown as Record<string,unknown>)[col.dataIndex];return {label:col.title,value:col.render?col.render(v,row,0):String(v??'—')};})})}>Xem</Button>}]}
          dataSource={q.data?.items || []}
          pagination={false}
          scroll={{ x: 900 }}
          locale={{ emptyText: <EmptyState description={emptyText} /> }}
        />
      )}
      <Paging page={page} total={q.data?.totalItems || 0} onChange={setPage} />
    </Card>
  );

  const bookingColumns: ColumnsType<import('../../services/landlordAdmin').BookingDto> = [
    { title: 'Tin đăng', dataIndex: 'propertyTitle', render: value => <Typography.Text strong>{value}</Typography.Text> },
    { title: 'Người thuê', dataIndex: 'tenantName', render: (value, row) => <Space orientation="vertical" size={0}>{value}<Typography.Text type="secondary">{row.tenantEmail}</Typography.Text></Space> },
    { title: 'Thời gian thuê', render: (_, row) => `${dateText(row.startDate)} – ${dateText(row.endDate)}` },
    { title: 'Giá thuê/tháng', dataIndex: 'monthlyRent', render: money },
    { title: 'Ước tính kỳ thuê', dataIndex: 'estimatedRent', render: money },
    { title: 'Trạng thái', dataIndex: 'status', render: value => <Status value={value} label={bookingStatusLabels[value] || String(value)} /> },
    { title: 'Ngày gửi', dataIndex: 'createdAt', render: dateText }
  ];
  const contractColumns: ColumnsType<import('../../services/landlordAdmin').ContractDto> = [
    { title: 'Mã hợp đồng', dataIndex: 'contractCode', render: value => <Typography.Text strong>{value}</Typography.Text> },
    { title: 'Tin đăng', dataIndex: 'propertyTitle' },
    { title: 'Chủ nhà / Người thuê', render: (_, row) => <Space orientation="vertical" size={0}>{row.landlordName}<Typography.Text type="secondary">{row.tenantName}</Typography.Text></Space> },
    { title: 'Thời hạn', render: (_, row) => `${dateText(row.startDate)} – ${dateText(row.endDate)}` },
    { title: 'Tiền thuê', dataIndex: 'monthlyRent', render: money },
    { title: 'Ước tính cả kỳ', dataIndex: 'estimatedRent', render: money },
    { title: 'Trạng thái', dataIndex: 'status', render: value => <Status value={value} label={contractStatusLabels[value] || value} /> }
  ];
  const invoiceColumns: ColumnsType<import('../../services/landlordAdmin').InvoiceListDto> = [
    { title: 'Mã hóa đơn', dataIndex: 'invoiceCode', render: value => <Typography.Text strong>{value}</Typography.Text> },
    { title: 'Hợp đồng', dataIndex: 'contractCode' },
    { title: 'Tin đăng', dataIndex: 'propertyTitle' },
    { title: 'Loại', dataIndex: 'invoiceType', render: value => value === 'Deposit' ? 'Tiền cọc' : value === 'MonthlyRent' ? 'Tiền thuê' : 'Khác' },
    { title: 'Hạn thanh toán', dataIndex: 'dueDate', render: dateText },
    { title: 'Tổng tiền', dataIndex: 'totalAmount', render: money },
    { title: 'Trạng thái', dataIndex: 'status', render: value => <Status value={value} label={invoiceStatusLabels[value] || value} /> }
  ];
  const paymentColumns: ColumnsType<AdminPaymentListDto> = [
    { title: 'Hóa đơn', dataIndex: 'invoiceCode', render: value => <Typography.Text strong>{value}</Typography.Text> },
    { title: 'Tin đăng', dataIndex: 'propertyTitle' },
    { title: 'Người thanh toán', dataIndex: 'payerName' },
    { title: 'Số tiền', dataIndex: 'amount', render: money },
    { title: 'Phương thức', dataIndex: 'paymentMethod' },
    { title: 'Mã giao dịch', dataIndex: 'transactionCode', render: value => value || '—' },
    { title: 'Trạng thái', dataIndex: 'status', render: value => <Status value={value} label={paymentStatusLabels[value] || value} /> },
    { title: 'Thời gian', dataIndex: 'createdAt', render: dateText }
  ];
  const reviewColumns: ColumnsType<AdminReviewDto> = [
    { title: 'Tin đăng', dataIndex: 'propertyTitle' },
    { title: 'Người đánh giá', dataIndex: 'tenantName' },
    { title: 'Hợp đồng', dataIndex: 'contractCode' },
    { title: 'Điểm', dataIndex: 'rating', render: value => <Tag color={value >= 4 ? 'success' : value >= 3 ? 'gold' : 'error'}>{value}/5 ★</Tag> },
    { title: 'Nội dung', dataIndex: 'comment', render: value => value || 'Không có nhận xét' },
    { title: 'Ngày đánh giá', dataIndex: 'createdAt', render: dateText }
  ];
  const activityColumns: ColumnsType<AdminModerationLogDto> = [
    { title: 'Tin đăng', dataIndex: 'propertyTitle' },
    { title: 'Quản trị viên', dataIndex: 'adminName' },
    { title: 'Thao tác', dataIndex: 'action', render: value => <Tag color={value === 'Approved' || value === 'Restored' ? 'success' : value === 'Rejected' ? 'error' : 'warning'}>{moderationActionLabels[({ Approved: 0, Rejected: 1, Hidden: 2, Restored: 3 } as Record<string, number>)[value]] || value}</Tag> },
    { title: 'Lý do', dataIndex: 'reason', render: value => value || '—' },
    { title: 'Thời gian', dataIndex: 'createdAt', render: dateText }
  ];

  const moveOutColumns:ColumnsType<AdminMoveOutDto>=[
 {title:'Hợp đồng',dataIndex:'contractCode'},{title:'Tin đăng',dataIndex:'propertyTitle'},{title:'Chủ nhà',dataIndex:'landlordName'},{title:'Người thuê',dataIndex:'tenantName'},{title:'Ngày trả nhà',dataIndex:'requestedMoveOutDate',render:dateText},{title:'Trạng thái',dataIndex:'status',render:v=><Tag>{moveOutLabels[v]||v}</Tag>},{title:'Hoàn tiền',dataIndex:'refundAmount',render:v=>v==null?'—':money(v)},{title:'Đóng thêm',dataIndex:'additionalAmount',render:v=>v==null?'—':money(v)},{title:'Chữ ký',render:(_,r)=><span>{Number(!!r.landlordConfirmedAt)+Number(!!r.tenantConfirmedAt)}/2</span>},{title:'Quyết toán',render:(_,r)=><Button size="small" onClick={()=>setRecord({title:'Biên bản trả nhà',fields:[{label:'Hợp đồng',value:r.contractCode},{label:'Lý do',value:r.reason},{label:'Ghi chú người thuê',value:r.tenantNotes||'—'},{label:'Ghi chú chủ nhà',value:r.landlordNotes||'—'},{label:'Hiện trạng',value:r.inspectionNotes||'—'},...([{label:'Cọc đã nhận',v:r.depositPaid},{label:'Tiền thuê phải trả',v:r.rentDue},{label:'Tiền thuê đã trả',v:r.rentPaid},{label:'Phí khác phải trả',v:r.otherDue},{label:'Phí khác đã trả',v:r.otherPaid},{label:'Phát sinh',v:r.extraCharges},{label:'Hoàn tiền',v:r.refundAmount},{label:'Đóng thêm',v:r.additionalAmount}].map(x=>({label:x.label,value:x.v==null?'—':money(x.v)}))),{label:'Diễn giải',value:r.settlementNotes||'—'},{label:'Chủ nhà ký',value:dateText(r.landlordConfirmedAt)},{label:'Người thuê ký',value:dateText(r.tenantConfirmedAt)},{label:'Hoàn tất',value:dateText(r.completedAt)}]})}>Biên bản</Button>}
 ];
 const statusOptions:Record<string,Array<{value:string;label:string}>>={
 bookings:['Pending','Accepted','Rejected','Cancelled','Expired'].map((value,i)=>({value,label:bookingStatusLabels[i]})),
 contracts:Object.entries(contractStatusLabels).map(([value,label])=>({value,label})),
 invoices:Object.entries(invoiceStatusLabels).map(([value,label])=>({value,label})),
 payments:Object.entries(paymentStatusLabels).map(([value,label])=>({value,label})),
 'move-outs':Object.entries(moveOutLabels).filter(([value])=>value!=='InspectionScheduled').map(([value,label])=>({value,label})),
 reviews:[1,2,3,4,5].map(v=>({value:String(v),label:v+' sao'})),
 activity:['Approved','Rejected','Hidden','Restored'].map((value,i)=>({value,label:moderationActionLabels[i]}))
 };
 const tabItems = [
    { key: 'bookings', label: 'Yêu cầu thuê', children: renderTable(bookings, bookingColumns, 'Chưa có yêu cầu thuê.') },
    { key: 'contracts', label: 'Hợp đồng', children: renderTable(contracts, contractColumns, 'Chưa có hợp đồng.') },
    { key: 'invoices', label: 'Hóa đơn', children: renderTable(invoices, invoiceColumns, 'Chưa có hóa đơn.') },
    { key: 'payments', label: 'Thanh toán', children: renderTable(payments, paymentColumns, 'Chưa có giao dịch thanh toán.') },
    { key:'move-outs',label:'Trả nhà',children:renderTable(moveOuts,moveOutColumns,'Chưa có yêu cầu trả nhà.') },
    { key: 'reviews', label: 'Đánh giá', children: renderTable(reviews, reviewColumns, 'Chưa có đánh giá.') },
    { key: 'activity', label: 'Nhật ký kiểm duyệt', children: renderTable(logs, activityColumns, 'Chưa có lịch sử kiểm duyệt.') }
  ];

  return (
    <Page title="Giám sát vận hành" description="Theo dõi toàn bộ yêu cầu thuê, hợp đồng, hóa đơn, thanh toán, đánh giá và lịch sử kiểm duyệt của hệ thống.">
      <div className="management-toolbar">
        <Input.Search
          allowClear
          value={keyword}
          onChange={event => { setPage(1); setKeyword(event.target.value); }}
          maxLength={100}
          placeholder="Tìm theo mã, tên người dùng, tin đăng..."
          style={{ maxWidth: 380, width: '100%' }}
        />
        <Select allowClear placeholder="Tất cả trạng thái" style={{minWidth:180}} value={status} options={statusOptions[activeTab]} onChange={v=>{setPage(1);setParams(v?{tab:activeTab,status:v}:{tab:activeTab});}}/>
        <Button onClick={()=>({bookings,contracts,invoices,payments,'move-outs':moveOuts,reviews,activity:logs}[activeTab]?.refresh())}>Làm mới</Button>
        <Tag>Chỉ đọc · giám sát</Tag>
      </div>
      <Card className="management-quick-card admin-operations">
        <Tabs activeKey={activeTab} onChange={switchTab} items={tabItems} />
      </Card>
      <Drawer title={record?.title} open={!!record} onClose={()=>setRecord(null)} styles={{wrapper:{width:'min(620px,100vw)'}}}><Descriptions bordered column={1} items={record?.fields.map((f,i)=>({key:i,label:f.label,children:f.value}))}/></Drawer>
    </Page>
  );
}

export function AdminUsers() {
  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState('');
  const [roleFilter,setRoleFilter]=useState<string|undefined>();
  const [activeFilter, setActiveFilter] = useState<string | undefined>();
  const q = useRequest(
    () => api.adminUsers({
      page,
      pageSize: 10,
      keyword: keyword.trim() || undefined,
      role:roleFilter,
      isActive: activeFilter === undefined ? undefined : activeFilter === 'true'
    }),
    [page, keyword, activeFilter,roleFilter]
  );
  const [selected, setSelected] = useState<AdminUserDto | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<AdminUserFormValues>();
  const { message } = AntApp.useApp();

  const openCreate = () => {
    setSelected(null);
    form.resetFields();
    form.setFieldsValue({ roles: ['Tenant'] });
    setDrawerOpen(true);
  };

  const openEdit = async (row: AdminUserDto) => {
    try {
      const detail = (await api.adminUser(row.userId)).data;
      setSelected(detail);
      form.setFieldsValue({
        fullName: detail.fullName,
        email: detail.email,
        phoneNumber: detail.phoneNumber || '',
        avatarUrl: detail.avatarUrl || '',
        roles: detail.roles || []
      });
      setDrawerOpen(true);
    } catch (error) {
      message.error(errorText(error));
    }
  };

  const save = async (values: AdminUserFormValues) => {
    setSaving(true);
    try {
      if (selected) {
        const profile: UpdateAdminUserRequest = {
          fullName: values.fullName.trim(),
          phoneNumber: values.phoneNumber?.trim() || undefined,
          avatarUrl: values.avatarUrl?.trim() || undefined
        };
        await api.updateAdminUser(selected.userId, profile);
        const currentRoles = [...(selected.roles || [])].sort().join('|');
        const nextRoles = [...values.roles].sort().join('|');
        if (currentRoles !== nextRoles)
          await api.updateUserRoles(selected.userId, values.roles);
        message.success('Đã cập nhật thông tin tài khoản.');
      } else {
        const request: CreateAdminUserRequest = {
          fullName: values.fullName.trim(),
          email: values.email!.trim(),
          password: values.password!,
          phoneNumber: values.phoneNumber?.trim() || undefined,
          avatarUrl: values.avatarUrl?.trim() || undefined,
          roles: values.roles
        };
        await api.createAdminUser(request);
        message.success('Đã tạo tài khoản mới.');
      }
      setDrawerOpen(false);
      form.resetFields();
      q.refresh();
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setSaving(false);
    }
  };

  const changeStatus = async (row: AdminUserDto, isActive: boolean) => {
    try {
      await api.updateUserStatus(row.userId, isActive);
      message.success(isActive ? 'Đã mở khóa tài khoản.' : 'Đã khóa tài khoản.');
      q.refresh();
    } catch (error) {
      message.error(errorText(error));
    }
  };


  const columns: ColumnsType<AdminUserDto> = [
    {
      title: 'Tài khoản',
      render: (_, row) => (
        <Space>
          <Avatar src={row.avatarUrl}>{row.fullName?.[0]}</Avatar>
          <Space orientation="vertical" size={0}>
            <Typography.Text strong>{row.fullName}</Typography.Text>
            <Typography.Text type="secondary">{row.email}</Typography.Text>
          </Space>
        </Space>
      )
    },
    { title: 'Điện thoại', dataIndex: 'phoneNumber', render: value => value || '—' },
    {
      title: 'Vai trò',
      dataIndex: 'roles',
      render: (roles: string[]) => (
        <Space wrap size={[4, 4]}>
          {(roles || []).map(role => <Tag key={role} color={role === 'Admin' ? 'purple' : role === 'Landlord' ? 'green' : 'blue'}>{adminRoleLabels[role]||role}</Tag>)}
        </Space>
      )
    },
    {
      title: 'Trạng thái',
      dataIndex: 'isActive',
      render: (active: boolean) => <Tag color={active ? 'success' : 'default'}>{active ? 'Hoạt động' : 'Đã khóa'}</Tag>
    },
    { title: 'Ngày tạo', dataIndex: 'createdAt', render: dateText },
    {
      title: 'Thao tác',
      fixed: 'right',
      render: (_, row) => (
        <Space wrap>
          <Button size="small" onClick={() => void openEdit(row)}>Sửa</Button>
          <Popconfirm title={row.isActive?'Khóa tài khoản này?':'Mở khóa tài khoản này?'} onConfirm={()=>void changeStatus(row,!row.isActive)}><Button size="small" danger={row.isActive}>{row.isActive?'Khóa':'Mở khóa'}</Button></Popconfirm>

        </Space>
      )
    }
  ];

  return (
    <Page title="Quản lý người dùng" description="Tạo tài khoản, cập nhật hồ sơ, phân quyền và khóa/mở khóa người dùng.">
      <ErrorLine error={q.error} />
      <div className="management-toolbar">
        <Input.Search
          allowClear
          value={keyword}
          onChange={event => { setPage(1); setKeyword(event.target.value); }}
          placeholder="Tìm theo tên, email hoặc số điện thoại"
          style={{ maxWidth: 360, width: '100%' }}
        />
        <Select
          allowClear
          value={activeFilter}
          placeholder="Tất cả trạng thái"
          onChange={value => { setPage(1); setActiveFilter(value); }}
          style={{ width: 170 }}
          options={[{ value: 'true', label: 'Hoạt động' }, { value: 'false', label: 'Đã khóa' }]}
        />
        <Select allowClear placeholder="Tất cả vai trò" style={{width:170}} value={roleFilter} onChange={v=>{setPage(1);setRoleFilter(v);}} options={Object.entries(adminRoleLabels).map(([value,label])=>({value,label}))}/>
        <Button onClick={q.refresh}>Làm mới</Button>
        <Button type="primary" onClick={openCreate}>Tạo tài khoản</Button>
      </div>
      <Card className="management-table-card">
        {q.busy ? <Loading /> : (
          <Table
            rowKey="userId"
            columns={columns}
            dataSource={q.data?.items || []}
            pagination={false}
            scroll={{ x: 950 }}
            locale={{ emptyText: <EmptyState description="Không tìm thấy tài khoản phù hợp." /> }}
          />
        )}
        <Paging page={page} total={q.data?.totalItems || 0} onChange={setPage} />
      </Card>

      <Drawer
        title={selected ? 'Cập nhật tài khoản' : 'Tạo tài khoản mới'}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        size="large"
        styles={{ wrapper: { width: 'min(560px, 100vw)' } }}
        extra={<Button type="primary" loading={saving} onClick={() => form.submit()}>Lưu thay đổi</Button>}
        destroyOnHidden
      >
        <Form form={form} layout="vertical" requiredMark={false} onFinish={save}>
          <Form.Item name="fullName" label="Họ và tên" rules={[{ required: true }, { min: 2, max: 150 }]}>
            <Input placeholder="Nhập họ và tên" />
          </Form.Item>
          {!selected && (
            <>
              <Form.Item name="email" label="Email đăng nhập" rules={[{ required: true }, { type: 'email' }, { max: 256 }]}>
                <Input autoComplete="email" placeholder="name@example.com" />
              </Form.Item>
              <Form.Item name="password" label="Mật khẩu khởi tạo" rules={[{ required: true }, { min: 8, max: 100 }]}>
                <Input.Password autoComplete="new-password" placeholder="Tối thiểu 8 ký tự" />
              </Form.Item>
            </>
          )}
          {selected && (
            <Form.Item label="Email đăng nhập">
              <Input value={selected.email} disabled />
            </Form.Item>
          )}
          <Form.Item name="phoneNumber" label="Số điện thoại">
            <Input placeholder="Nhập số điện thoại" />
          </Form.Item>
          <Form.Item name="avatarUrl" label="Ảnh đại diện (URL)" rules={[{ type: 'url' }]}>
            <Input placeholder="https://..." />
          </Form.Item>
          <Form.Item name="roles" label="Vai trò" rules={[{ required: true, type: 'array', min: 1, message: 'Tài khoản cần ít nhất một vai trò.' }]}>
            <Select mode="multiple" options={[
              { value: 'Tenant', label: 'Người thuê' },
              { value: 'Landlord', label: 'Chủ nhà' },
              { value: 'Admin', label: 'Quản trị viên' }
            ]} />
          </Form.Item>
          {selected && <Alert type="info" showIcon message="Thay đổi vai trò có hiệu lực ngay ở API; người dùng cần đăng nhập lại để nhận JWT mới." />}
        </Form>
      </Drawer>
    </Page>
  );
}

export function AdminModeration() {
  const [page,setPage]=useState(1); const [modParams]=useSearchParams(); const [status,setStatus]=useState<string|undefined>(modParams.get('status')==='all'?undefined:modParams.get('status')||'Pending'); const [listingStatus,setListingStatus]=useState<string|undefined>(); const [keyword,setKeyword]=useState(''); const [province,setProvince]=useState(''); const q=useRequest(()=>api.moderationProperties({page,pageSize:10,status,listingStatus,keyword:keyword||undefined,province:province||undefined}),[page,status,listingStatus,keyword,province]); const [detail,setDetail]=useState<ModerationPropertyDto|null>(null); const {message,modal}=AntApp.useApp();
  const refreshDetail=async(id:string)=>{setDetail((await api.moderationDetail(id)).data);q.refresh();};
  const reasonAction=(title:string,fn:(id:string,reason:string)=>Promise<unknown>,row:ModerationPropertyDto)=>modal.confirm({title,content:<Input.TextArea id="moderation-reason" placeholder="Lý do (ít nhất 5 ký tự)" rows={3}/>,onOk:async()=>{const reason=(document.getElementById('moderation-reason') as HTMLTextAreaElement)?.value||'';if(reason.trim().length<5){message.error('Lý do cần ít nhất 5 ký tự');throw new Error('Thiếu lý do');}try{await fn(row.id,reason);message.success('Đã cập nhật trạng thái kiểm duyệt');q.refresh();if(detail?.id===row.id)await refreshDetail(row.id);}catch(e){message.error(errorText(e));throw e;}}});
  const columns:ColumnsType<ModerationPropertyDto>=[{title:'Tin đăng',dataIndex:'title'},{title:'Chủ nhà',dataIndex:'landlordName'},{title:'Khu vực',dataIndex:'provinceName'},{title:'Giá thuê',dataIndex:'pricePerMonth',render:money},{title:'Kiểm duyệt',dataIndex:'moderationStatus',render:(v:number)=><Status value={v} label={moderationStatusLabels[v]||String(v)}/>},{title:'Hiển thị',dataIndex:'listingStatus',render:v=><Status value={v} label={listingStatusLabels[String(v)]}/>},{title:'Thao tác',render:(_,r)=><Space wrap><Button size="small" onClick={async()=>{try{setDetail((await api.moderationDetail(r.id)).data);}catch(e){message.error(errorText(e));}}}>Chi tiết</Button>{r.moderationStatus===1&&<><Popconfirm title="Duyệt tin đăng?" onConfirm={async()=>{try{await api.approveProperty(r.id);message.success('Đã duyệt');q.refresh();}catch(e){message.error(errorText(e));}}}><Button size="small" type="primary">Duyệt</Button></Popconfirm><Button size="small" danger onClick={()=>reasonAction('Từ chối tin đăng',api.rejectProperty,r)}>Từ chối</Button></>}{r.moderationStatus===2&&r.listingStatus===1&&<Button size="small" danger onClick={()=>reasonAction('Ẩn tin đăng',api.hideProperty,r)}>Ẩn</Button>}{r.moderationStatus===2&&r.listingStatus===2&&<Button size="small" onClick={()=>reasonAction('Khôi phục tin đăng',api.restoreProperty,r)}>Khôi phục</Button>}</Space>}];
  return <Page title="Kiểm duyệt tin đăng"><ErrorLine error={q.error}/><Space className="management-toolbar" wrap style={{marginBottom:16}}><Select allowClear placeholder="Tất cả kiểm duyệt" value={status} onChange={v=>{setPage(1);setStatus(v);}} style={{width:180}} options={['Draft','Pending','Approved','Rejected'].map(v=>({value:v,label:moderationStatusLabels[({Draft:0,Pending:1,Approved:2,Rejected:3} as Record<string,number>)[v]]}))}/><Select allowClear placeholder="Trạng thái hiển thị" style={{width:190}} value={listingStatus} onChange={v=>{setPage(1);setListingStatus(v);}} options={['Draft','Published','Hidden','Closed'].map(v=>({value:v,label:listingStatusLabels[v]}))}/><Input placeholder="Tìm theo tiêu đề" value={keyword} onChange={e=>{setPage(1);setKeyword(e.target.value);}} style={{width:220}}/><Input placeholder="Tỉnh/thành phố" value={province} onChange={e=>{setPage(1);setProvince(e.target.value);}} style={{width:180}}/></Space><Card className="management-table-card">{q.busy?<Loading/>:<Table scroll={{x:900}} rowKey="id" columns={columns} dataSource={q.data?.items||[]} pagination={false} locale={{emptyText:<EmptyState/>}}/>}<Paging page={page} total={q.data?.totalCount||0} onChange={setPage}/></Card><Drawer title="Chi tiết và lịch sử kiểm duyệt" open={!!detail} onClose={()=>setDetail(null)} size="large" styles={{ wrapper: { width: 'min(600px, 100vw)' } }}>{detail&&<><Descriptions bordered column={1} items={[{key:'1',label:'Tiêu đề',children:detail.title},{key:'2',label:'Chủ nhà',children:detail.landlordName},{key:'type',label:'Loại nhà',children:detail.propertyTypeName||detail.propertyTypeId||'—'},{key:'area',label:'Diện tích',children:detail.area?detail.area+' m²':'—'},{key:'bedrooms',label:'Phòng ngủ',children:detail.bedrooms??'—'},{key:'bathrooms',label:'Phòng tắm',children:detail.bathrooms??'—'},{key:'3',label:'Địa chỉ',children:[detail.addressLine,detail.wardName,detail.provinceName].filter(Boolean).join(', ')},{key:'4',label:'Giá',children:money(detail.pricePerMonth)},{key:'5',label:'Mô tả',children:detail.description||'—'},{key:'6',label:'Trạng thái',children:<><Status value={detail.moderationStatus} label={moderationStatusLabels[detail.moderationStatus]||String(detail.moderationStatus)}/><Status value={detail.listingStatus} label={listingStatusLabels[String(detail.listingStatus)]||String(detail.listingStatus)}/></>} ]}/><Typography.Title level={5}>Hình ảnh</Typography.Title><Space wrap>{(detail.images||[]).map((img:PropertyImageDto)=><Avatar key={img.id} shape="square" size={90} src={img.imageUrl}/>)}</Space><Typography.Title level={5}>Tiện ích</Typography.Title><Typography.Paragraph>{detail.amenities?.join(', ')||'Chưa có tiện ích được ghi nhận.'}</Typography.Paragraph><Typography.Title level={5}>Lịch sử kiểm duyệt</Typography.Title><List dataSource={detail.moderationLogs||[]} locale={{emptyText:'Chưa có lịch sử'}} renderItem={(l:NonNullable<ModerationPropertyDto['moderationLogs']>[number])=><List.Item><List.Item.Meta title={(moderationActionLabels[l.action]||String(l.action))+' · '+l.adminName} description={l.reason||'Không có lý do'}/>{dateText(l.createdAt)}</List.Item>}/></>}</Drawer></Page>;
}
