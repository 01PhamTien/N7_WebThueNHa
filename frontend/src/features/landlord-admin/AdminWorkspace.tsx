import { useState } from 'react';
import {
  Alert,
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
  Tag,
  Typography
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { errorText } from '../../services/api';
import { landlordAdminApi as api } from '../../services/landlordAdmin';
import type {
  AdminUserDto,
  CreateAdminUserRequest,
  ModerationPropertyDto,
  PropertyImageDto,
  UpdateAdminUserRequest
} from '../../services/landlordAdmin';
import { Page, Loading, EmptyState } from '../../components/Shared';
import {
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

export function AdminDashboard() {
  const q = useRequest(() => api.adminDashboard(), []);
  const metrics = [
    { title: 'Tổng tài khoản', value: q.data?.totalUsers ?? 0, note: `${q.data?.activeUsers ?? 0} đang hoạt động` },
    { title: 'Tổng tin đăng', value: q.data?.totalProperties ?? 0, note: `${q.data?.publishedProperties ?? 0} đang công khai` },
    { title: 'Tin chờ duyệt', value: q.data?.pendingProperties ?? 0, note: 'Cần kiểm tra nội dung' },
    { title: 'Yêu cầu thuê chờ xử lý', value: q.data?.pendingBookings ?? 0, note: 'Trên toàn hệ thống' },
    { title: 'Hợp đồng hiệu lực', value: q.data?.activeContracts ?? 0, note: 'Đang có hiệu lực' },
    { title: 'Hóa đơn quá hạn', value: q.data?.overdueInvoices ?? 0, note: 'Cần theo dõi thanh toán' }
  ];

  return (
    <Page title="Tổng quan quản trị" description="Theo dõi tình hình tài khoản, tin đăng và hoạt động thuê nhà trên toàn hệ thống.">
      <ErrorLine error={q.error} />
      <Row gutter={[16, 16]}>
        {metrics.map(item => (
          <Col xs={12} md={8} xl={4} key={item.title}>
            <Card className="management-stat-card">
              <Statistic title={item.title} value={item.value} loading={q.busy} />
              <Typography.Text type="secondary">{item.note}</Typography.Text>
            </Card>
          </Col>
        ))}
      </Row>
      <Card className="management-quick-card" title="Tác vụ quản trị">
        <div className="management-quick-actions">
          <Button type="primary" href="/admin/users">Quản lý tài khoản</Button>
          <Button href="/admin/moderation">Kiểm duyệt và quản lý tin đăng</Button>
        </div>
      </Card>
    </Page>
  );
}

export function AdminUsers() {
  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState('');
  const [activeFilter, setActiveFilter] = useState<string | undefined>();
  const q = useRequest(
    () => api.adminUsers({
      page,
      pageSize: 10,
      keyword: keyword.trim() || undefined,
      isActive: activeFilter === undefined ? undefined : activeFilter === 'true'
    }),
    [page, keyword, activeFilter]
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

  const deactivate = async (row: AdminUserDto) => {
    try {
      await api.deleteAdminUser(row.userId);
      message.success('Đã ngừng hoạt động tài khoản. Lịch sử giao dịch được giữ nguyên.');
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
          <Space direction="vertical" size={0}>
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
          {(roles || []).map(role => <Tag key={role} color={role === 'Admin' ? 'purple' : role === 'Landlord' ? 'green' : 'blue'}>{role}</Tag>)}
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
          <Button size="small" onClick={() => void changeStatus(row, !row.isActive)}>
            {row.isActive ? 'Khóa' : 'Mở khóa'}
          </Button>
          {row.isActive && (
            <Popconfirm
              title="Ngừng hoạt động tài khoản này?"
              description="Tài khoản sẽ không thể đăng nhập; lịch sử thuê và thanh toán vẫn được giữ."
              okText="Ngừng hoạt động"
              cancelText="Hủy"
              onConfirm={() => void deactivate(row)}
            >
              <Button size="small" danger>Ngừng hoạt động</Button>
            </Popconfirm>
          )}
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
  const [page,setPage]=useState(1); const [status,setStatus]=useState('Pending'); const [listingStatus,setListingStatus]=useState<string|undefined>(); const [keyword,setKeyword]=useState(''); const [province,setProvince]=useState(''); const q=useRequest(()=>api.moderationProperties({page,pageSize:10,status,listingStatus,keyword:keyword||undefined,province:province||undefined}),[page,status,listingStatus,keyword,province]); const [detail,setDetail]=useState<ModerationPropertyDto|null>(null); const {message,modal}=AntApp.useApp();
  const refreshDetail=async(id:string)=>{setDetail((await api.moderationDetail(id)).data);q.refresh();};
  const reasonAction=(title:string,fn:(id:string,reason:string)=>Promise<unknown>,row:ModerationPropertyDto)=>modal.confirm({title,content:<Input.TextArea id="moderation-reason" placeholder="Lý do (ít nhất 5 ký tự)" rows={3}/>,onOk:async()=>{const reason=(document.getElementById('moderation-reason') as HTMLTextAreaElement)?.value||'';if(reason.trim().length<5){message.error('Lý do cần ít nhất 5 ký tự');throw new Error('Thiếu lý do');}try{await fn(row.id,reason);message.success('Đã cập nhật trạng thái kiểm duyệt');q.refresh();if(detail?.id===row.id)await refreshDetail(row.id);}catch(e){message.error(errorText(e));throw e;}}});
  const columns:ColumnsType<ModerationPropertyDto>=[{title:'Tin đăng',dataIndex:'title'},{title:'Chủ nhà',dataIndex:'landlordName'},{title:'Khu vực',dataIndex:'provinceName'},{title:'Giá thuê',dataIndex:'pricePerMonth',render:money},{title:'Trạng thái',dataIndex:'moderationStatus',render:(v:number)=><Status value={v} label={moderationStatusLabels[v]||String(v)}/>},{title:'Thao tác',render:(_,r)=><Space wrap><Button size="small" onClick={async()=>{try{setDetail((await api.moderationDetail(r.id)).data);}catch(e){message.error(errorText(e));}}}>Chi tiết</Button>{r.moderationStatus===1&&<><Popconfirm title="Duyệt tin đăng?" onConfirm={async()=>{try{await api.approveProperty(r.id);message.success('Đã duyệt');q.refresh();}catch(e){message.error(errorText(e));}}}><Button size="small" type="primary">Duyệt</Button></Popconfirm><Button size="small" danger onClick={()=>reasonAction('Từ chối tin đăng',api.rejectProperty,r)}>Từ chối</Button></>}{r.moderationStatus===2&&r.listingStatus===1&&<Button size="small" danger onClick={()=>reasonAction('Ẩn tin đăng',api.hideProperty,r)}>Ẩn</Button>}{r.moderationStatus===2&&r.listingStatus===2&&<Button size="small" onClick={()=>reasonAction('Khôi phục tin đăng',api.restoreProperty,r)}>Khôi phục</Button>}</Space>}];
  return <Page title="Kiểm duyệt tin đăng"><ErrorLine error={q.error}/><Space wrap style={{marginBottom:16}}><Select value={status} onChange={v=>{setPage(1);setStatus(v);}} style={{width:180}} options={['Draft','Pending','Approved','Rejected'].map(v=>({value:v,label:moderationStatusLabels[({Draft:0,Pending:1,Approved:2,Rejected:3} as Record<string,number>)[v]]}))}/><Select allowClear placeholder="Trạng thái hiển thị" style={{width:190}} value={listingStatus} onChange={v=>{setPage(1);setListingStatus(v);if(v==='Hidden'||v==='Published'||v==='Closed')setStatus('Approved');else if(v==='Draft')setStatus('Pending');}} options={['Draft','Published','Hidden','Closed'].map(v=>({value:v,label:listingStatusLabels[v]}))}/><Input placeholder="Tìm theo tiêu đề" value={keyword} onChange={e=>{setPage(1);setKeyword(e.target.value);}} style={{width:220}}/><Input placeholder="Tỉnh/thành phố" value={province} onChange={e=>{setPage(1);setProvince(e.target.value);}} style={{width:180}}/></Space><Card>{q.busy?<Loading/>:<Table rowKey="id" columns={columns} dataSource={q.data?.items||[]} pagination={false} locale={{emptyText:<EmptyState/>}}/>}<Paging page={page} total={q.data?.totalCount||0} onChange={setPage}/></Card><Drawer title="Chi tiết và lịch sử kiểm duyệt" open={!!detail} onClose={()=>setDetail(null)} size="large" styles={{ wrapper: { width: 'min(600px, 100vw)' } }}>{detail&&<><Descriptions bordered column={1} items={[{key:'1',label:'Tiêu đề',children:detail.title},{key:'2',label:'Chủ nhà',children:detail.landlordName},{key:'type',label:'Loại nhà',children:detail.propertyTypeName||detail.propertyTypeId||'—'},{key:'area',label:'Diện tích',children:detail.area?detail.area+' m²':'—'},{key:'bedrooms',label:'Phòng ngủ',children:detail.bedrooms??'—'},{key:'bathrooms',label:'Phòng tắm',children:detail.bathrooms??'—'},{key:'3',label:'Địa chỉ',children:[detail.addressLine,detail.wardName,detail.provinceName].filter(Boolean).join(', ')},{key:'4',label:'Giá',children:money(detail.pricePerMonth)},{key:'5',label:'Mô tả',children:detail.description||'—'},{key:'6',label:'Trạng thái',children:<><Status value={detail.moderationStatus} label={moderationStatusLabels[detail.moderationStatus]||String(detail.moderationStatus)}/><Status value={detail.listingStatus} label={listingStatusLabels[String(detail.listingStatus)]||String(detail.listingStatus)}/></>} ]}/><Typography.Title level={5}>Hình ảnh</Typography.Title><Space wrap>{(detail.images||[]).map((img:PropertyImageDto)=><Avatar key={img.id} shape="square" size={90} src={img.imageUrl}/>)}</Space><Typography.Title level={5}>Tiện ích</Typography.Title><Typography.Paragraph>{detail.amenities?.join(', ')||'Chưa có tiện ích được ghi nhận.'}</Typography.Paragraph><Typography.Title level={5}>Lịch sử kiểm duyệt</Typography.Title><List dataSource={detail.moderationLogs||[]} locale={{emptyText:'Chưa có lịch sử'}} renderItem={(l:NonNullable<ModerationPropertyDto['moderationLogs']>[number])=><List.Item><List.Item.Meta title={(moderationActionLabels[l.action]||String(l.action))+' · '+l.adminName} description={l.reason||'Không có lý do'}/>{dateText(l.createdAt)}</List.Item>}/></>}</Drawer></Page>;
}
