import { useEffect, useRef, useState } from 'react';
import { Alert, App as AntApp, Avatar, Badge, Button, Card, Col, DatePicker, Descriptions, Drawer, Empty, Form, Input, InputNumber, List, Modal, Popconfirm, Progress, Row, Segmented, Select, Space, Statistic, Table, Tag, Typography, Upload } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { BellOutlined } from '@ant-design/icons';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { TOKEN_KEY, errorText } from '../../services/api';
import { landlordAdminApi as api } from '../../services/landlordAdmin';
import type { AdminPaymentListDto, BookingDto, ChatAttachmentDto, ChatMessageDto, ContractDto, ContractDetailDto, ConversationDto, InvoiceDetailDto, InvoiceListDto, PaymentDto, PropertyDto, PropertyImageDto, PropertyRequest, CreateInvoiceRequest, NotificationDto } from '../../services/landlordAdmin';
import { Page, Loading, EmptyState } from '../../components/Shared';
import { bookingStatusLabels, contractStatusLabels, invoiceStatusLabels, invoiceTypeLabels, listingStatusLabels, moderationStatusLabels, paymentStatusLabels, money, dateText, Status, ErrorLine, Paging, useRequest } from './workspaceShared';

interface ContractFormValues { startDate: Dayjs; endDate: Dayjs; monthlyRent: number; depositAmount: number; paymentDueDay: number; terms?: string; }
interface InvoiceFormValues { invoiceType: number; issueDate: string; dueDate: string; periodStart?: string; periodEnd?: string; description: string; quantity: number; unitPrice: number; }

function invoiceDueHint(dueDate: string, status: string) {
  if (status === 'Paid' || status === 'Cancelled') return null;
  const [year, month, day] = dueDate.slice(0, 10).split('-').map(Number);
  if (!year || !month || !day) return null;
  const due = new Date(year, month - 1, day);
  const nowVietnam = new Date(Date.now() + 7 * 60 * 60 * 1000);
  const todayKey = Date.UTC(nowVietnam.getUTCFullYear(), nowVietnam.getUTCMonth(), nowVietnam.getUTCDate());
  const dueKey = Date.UTC(year, month - 1, day);
  const days = Math.round((dueKey - todayKey) / 86400000);
  if (days < 0) return <Tag color="red">Quá hạn {Math.abs(days)} ngày</Tag>;
  if (days === 0) return <Tag color="orange">Đến hạn hôm nay</Tag>;
  return <Tag color={days <= 7 ? 'orange' : 'blue'}>Còn {days} ngày</Tag>;
}

export function LandlordDashboard() {
  const financial=useRequest(()=>api.landlordFinancialSummary(12),[]);
  const bookings=useRequest(()=>api.incomingBookings({page:1,pageSize:5,status:'Pending'}),[]);
  const signing=useRequest(()=>api.contracts({page:1,pageSize:1,status:'AwaitingSignature'}),[]);
  const overdue=useRequest(()=>api.invoices({page:1,pageSize:5,status:'Overdue'}),[]);
  const pending=useRequest(()=>api.incomingPayments({page:1,pageSize:5,status:'Pending'}),[]);
  const [months,setMonths]=useState(6);
  const data=financial.data;
  const trend=(data?.trend||[]).slice(-months).reverse();
  const max=Math.max(1,...trend.map(x=>x.received));
  const error=[financial,bookings,signing,overdue,pending].map(x=>x.error).filter(Boolean).join(' · ');
  const tasks=[
    {label:'Yêu cầu thuê chờ xử lý',count:bookings.data?.totalCount??bookings.data?.totalItems,href:'/landlord/bookings',color:'gold'},
    {label:'Hợp đồng chờ ký',count:signing.data?.totalItems,href:'/landlord/contracts',color:'blue'},
    {label:'Thanh toán chờ xác nhận',count:pending.data?.totalItems,href:'/landlord/payments',color:'blue'},
    {label:'Hóa đơn quá hạn',count:overdue.data?.totalItems,href:'/landlord/invoices',color:'red'},
    {label:'Hợp đồng sắp hết hạn (30 ngày)',count:data?.endingSoon,href:'/landlord/contracts',color:'orange'}
  ];
  const recent=[
    ...(bookings.data?.items||[]).map(x=>({id:x.id,title:x.propertyTitle,description:x.tenantName,kind:'Yêu cầu thuê',createdAt:x.createdAt,href:'/landlord/bookings'})),
    ...(pending.data?.items||[]).map(x=>({id:x.id,title:x.propertyTitle,description:x.payerName+' · '+money(x.amount),kind:'Thanh toán',createdAt:x.createdAt,href:'/landlord/payments'})),
    ...(overdue.data?.items||[]).map(x=>({id:x.id,title:x.propertyTitle,description:x.invoiceCode,kind:'Hóa đơn quá hạn',createdAt:x.createdAt,href:'/landlord/invoices'}))
  ].sort((a,b)=>Date.parse(b.createdAt)-Date.parse(a.createdAt)||b.id.localeCompare(a.id)).slice(0,5);
  return <Page title="Tổng quan chủ nhà" description="Theo dõi hoạt động cho thuê và tài chính tại một nơi.">
    <ErrorLine error={error}/>
    <Row gutter={[12,12]}>
      <Col xs={12} lg={6}><Card><Statistic title="Tin đăng" value={data?.propertyCount??'—'} loading={financial.busy}/><Typography.Link href="/landlord/properties">Quản lý</Typography.Link></Card></Col>
      <Col xs={12} lg={6}><Card><Statistic title="Hợp đồng hiệu lực" value={data?.activeContracts??'—'} loading={financial.busy}/><Typography.Link href="/landlord/contracts">Quản lý</Typography.Link></Card></Col>
      <Col xs={12} lg={6}><Card><Statistic title="Tiền thu tháng này" value={data?money(data.receivedThisMonth):'—'} loading={financial.busy}/><Typography.Text type="secondary">Bao gồm tiền cọc</Typography.Text></Card></Col>
      <Col xs={12} lg={6}><Card><Statistic title="Công nợ còn phải thu" value={data?money(data.outstandingAmount):'—'} loading={financial.busy}/><Typography.Text type="secondary">Quá hạn: {data?money(data.overdueAmount):'—'}</Typography.Text></Card></Col>
    </Row>
    <Row gutter={[16,16]} style={{marginTop:16}}>
      <Col xs={24} xl={14}><Card title="Doanh thu theo tháng" extra={<Segmented value={months} onChange={v=>setMonths(Number(v))} options={[{label:'6 tháng',value:6},{label:'12 tháng',value:12}]}/>}>
        {financial.busy?<Loading/>:!data?<Empty description="Chưa tải được số liệu"/>:<>
          <Row gutter={[12,12]} style={{marginBottom:16}}>
            <Col xs={24} sm={12}><Statistic title="Tiền thuê đã thu (lũy kế)" value={money(data.rentReceived)}/></Col>
            <Col xs={24} sm={12}><Statistic title="Tiền cọc đã nhận (lũy kế)" value={money(data.depositReceived)}/></Col>
          </Row>
          {trend.map(item=><div key={item.label} style={{marginBottom:8}}><Space style={{width:'100%',justifyContent:'space-between'}}><Typography.Text>{item.label}</Typography.Text><Typography.Text strong>{money(item.received)}</Typography.Text></Space><Progress percent={Math.round(item.received/max*100)} showInfo={false}/></div>)}
          <Typography.Text type="secondary">Giao dịch thành công, không gồm tiền cọc. Tháng mới nhất ở trên.</Typography.Text>
        </>}
      </Card></Col>
      <Col xs={24} xl={10}><Card title="Việc cần xử lý">
        {tasks.map(t=><div key={t.label} style={{display:'flex',alignItems:'center',justifyContent:'space-between',gap:12,padding:'12px 0',borderBottom:'1px solid #e8e8e8'}}><Typography.Link href={t.href}>{t.label}</Typography.Link><Tag color={t.count?t.color:'default'}>{t.count??'—'}</Tag></div>)}
      </Card><Card title="Hồ sơ cần xử lý mới nhất" style={{marginTop:16}}>
        {bookings.busy||pending.busy||overdue.busy?<Loading/>:recent.length===0?<Empty description="Không có hồ sơ cần xử lý"/>:recent.map(x=><div key={x.kind+x.id} style={{display:'flex',justifyContent:'space-between',gap:12,padding:'10px 0',borderBottom:'1px solid #e8e8e8'}}><div><Typography.Link href={x.href}>{x.title}</Typography.Link><div><Typography.Text type="secondary">{x.description}</Typography.Text></div></div><Tag>{x.kind}</Tag></div>)}
      </Card></Col>
    </Row>
  </Page>;
}

const propertyFields = [
  {name:'propertyTypeId',label:'Loại nhà',required:true},
  {name:'title',label:'Tiêu đề',required:true},
  {name:'description',label:'Mô tả'},
  {name:'pricePerMonth',label:'Giá niêm yết/tháng',required:true},
  {name:'depositAmount',label:'Tiền cọc (đ)',required:true},
  {name:'maxOccupants',label:'Số người ở tối đa',required:true},
  {name:'furnishingDescription',label:'Nội thất và trang bị'},
  {name:'additionalFeesDescription',label:'Điện, nước và phí khác'},
  {name:'area',label:'Diện tích (m²)',required:true},
  {name:'bedrooms',label:'Phòng ngủ',required:true},
  {name:'bathrooms',label:'Phòng tắm',required:true},
  {name:'addressLine',label:'Địa chỉ',required:true},
  {name:'wardName',label:'Phường/Xã'},
  {name:'provinceName',label:'Tỉnh/Thành phố',required:true}
];
export function LandlordProperties() {
  const [page,setPage]=useState(1); const [status,setStatus]=useState<string|undefined>(); const [keyword,setKeyword]=useState('');
  const q=useRequest(()=>api.myProperties({page,pageSize:10,status,keyword:keyword||undefined}),[page,status,keyword]);
  const [editing,setEditing]=useState<PropertyDto|null>(null); const [images,setImages]=useState<PropertyDto|null>(null);
  const [form]=Form.useForm(); const {message}=AntApp.useApp();
  const openEdit=(row?:PropertyDto)=>{setEditing(row||({} as PropertyDto));form.setFieldsValue(row||{propertyTypeId:1,bedrooms:0,bathrooms:0,depositAmount:0,maxOccupants:2});};
  const save=async(v:PropertyRequest)=>{try{if(editing?.id) await api.updateProperty(editing.id,v); else await api.createProperty(v);message.success('Đã lưu tin đăng. Tin sẽ ở trạng thái chờ duyệt sau khi tạo/cập nhật.');setEditing(null);form.resetFields();await q.refresh();}catch(e){message.error(errorText(e));}};
  const columns:ColumnsType<PropertyDto>=[
    {title:'Tin đăng',dataIndex:'title',render:(v,r)=><Space orientation="vertical" size={0}><b>{v}</b><Typography.Text type="secondary">{r.addressLine}, {r.provinceName}</Typography.Text></Space>},
    {title:'Giá thuê',dataIndex:'pricePerMonth',render:money},
    {title:'Kiểm duyệt',dataIndex:'moderationStatus',render:(v:string)=><Status value={v} label={moderationStatusLabels[({ Draft: 0, Pending: 1, Approved: 2, Rejected: 3 } as Record<string, number>)[v]] || v}/>},
    {title:'Hiển thị',dataIndex:'listingStatus',render:(v:string)=><Status value={v} label={listingStatusLabels[v] || v}/>},
    {title:'Tình trạng thuê',dataIndex:'rentalStatus',render:(v:string)=><Tag color={v==='Rented'?'default':v==='Reserved'?'warning':v==='PendingRequest'?'processing':'success'}>{v==='Rented'?'Đang cho thuê':v==='Reserved'?'Đã có lịch thuê':v==='PendingRequest'?'Có yêu cầu thuê':'Đang trống'}</Tag>},
    {title:'Thao tác',render:(_,r)=><Space wrap><Button size="small" onClick={()=>openEdit(r)}>Sửa</Button><Button size="small" onClick={()=>setImages(r)}>Ảnh</Button><Select size="small" style={{width:145}} value={r.listingStatus === 'Published' ? 1 : r.listingStatus === 'Hidden' ? 2 : undefined} options={[{value:1,label:'Đăng công khai'},{value:2,label:'Ẩn tin'}]} onChange={async v=>{try{await api.updatePropertyStatus(r.id,v);message.success('Đã cập nhật trạng thái');q.refresh();}catch(e){message.error(errorText(e));}}}/>{r.listingStatus === 'Closed' ? <Popconfirm title="Mở lại tin đăng này?" onConfirm={async()=>{try{await api.updatePropertyStatus(r.id,1);message.success('Đã mở lại tin đăng');q.refresh();}catch(e){message.error(errorText(e));}}}><Button size="small">Mở lại</Button></Popconfirm> : <Popconfirm title="Đóng tin đăng này?" description="Tin sẽ ngừng hiển thị công khai nhưng vẫn được lưu trong hệ thống." onConfirm={async()=>{try{await api.updatePropertyStatus(r.id,3);message.success('Đã đóng tin đăng');q.refresh();}catch(e){message.error(errorText(e));}}}><Button size="small" danger>Đóng tin</Button></Popconfirm>}</Space>}
  ];
  return <Page title="Quản lý tin đăng" description="Tin vừa tạo hoặc chỉnh sửa sẽ chuyển về Draft và cần Admin duyệt lại."><ErrorLine error={q.error}/><Space className="management-toolbar" style={{marginBottom:16}} wrap><Input.Search value={keyword} onChange={e=>{setPage(1);setKeyword(e.target.value);}} placeholder="Tìm tiêu đề hoặc địa chỉ" style={{width:280}}/><Select allowClear placeholder="Lọc trạng thái" style={{width:180}} value={status} onChange={v=>{setPage(1);setStatus(v);}} options={['Draft','Published','Hidden','Closed'].map(v=>({value:v,label:listingStatusLabels[v]}))}/><Button type="primary" onClick={()=>openEdit()}>Tạo tin đăng</Button></Space><Card className="management-table-card">{q.busy?<Loading/>:<Table rowKey="id" columns={columns} dataSource={q.data?.items||[]} pagination={false} locale={{emptyText:<EmptyState/>}}/>}<Paging page={page} total={q.data?.totalCount||0} onChange={setPage}/></Card>
    <Drawer title={editing?.id?'Cập nhật tin đăng':'Tạo tin đăng'} open={!!editing} size="large" styles={{ wrapper: { width: 'min(560px, 100vw)' } }} onClose={()=>setEditing(null)} extra={<Button type="primary" onClick={()=>form.submit()}>Lưu</Button>}><Form form={form} layout="vertical" onFinish={save}>{propertyFields.map(f=><Form.Item key={f.name} name={f.name} label={f.label} rules={f.required?[{required:true,message:'Vui lòng nhập '+f.label.toLowerCase()}]:[]}>
      {f.name==='propertyTypeId'?<Select placeholder="Chọn loại nhà" options={[{value:1,label:'Phòng trọ'},{value:2,label:'Căn hộ'},{value:3,label:'Nhà nguyên căn'},{value:4,label:'Studio'},{value:5,label:'Mặt bằng'}]}/>:['bedrooms','bathrooms','maxOccupants'].includes(f.name)?<InputNumber style={{width:'100%'}} min={f.name==='maxOccupants'?1:0} precision={0} placeholder={f.label}/>:['pricePerMonth','depositAmount','area'].includes(f.name)?<InputNumber style={{width:'100%'}} min={f.name==='depositAmount'?0:0.01} precision={2} placeholder={f.label}/> :['description','furnishingDescription','additionalFeesDescription'].includes(f.name)?<Input.TextArea rows={3} maxLength={1000} placeholder={f.name==='furnishingDescription'?'Ví dụ: Giường, nệm, tủ quần áo, máy lạnh, bàn làm việc':f.name==='additionalFeesDescription'?'Ví dụ: Điện 3.500 đ/kWh, nước 80.000 đ/người/tháng, Wi-Fi miễn phí':'Mô tả căn nhà'}/>:<Input placeholder={f.label}/>}
    </Form.Item>)}</Form></Drawer>
    <Modal title={'Quản lý ảnh: '+images?.title} open={!!images} onCancel={()=>setImages(null)} footer={null}>
      <Space direction="vertical" style={{width:'100%'}}>
        <Typography.Text type="secondary">Tải ảnh JPG, PNG hoặc WEBP, tối đa 8 MB mỗi ảnh và 20 ảnh cho mỗi tin đăng.</Typography.Text>
        <Upload accept="image/jpeg,image/png,image/webp" multiple showUploadList={false} beforeUpload={file=>{
          if(!images)return Upload.LIST_IGNORE;
          if((images.images.length||0)>=20){message.error('Mỗi tin đăng tối đa 20 hình ảnh.');return Upload.LIST_IGNORE;}
          if(file.size>8*1024*1024){message.error('Mỗi hình ảnh tối đa 8 MB.');return Upload.LIST_IGNORE;}
          void api.uploadPropertyImage(images.id,file).then(async()=>{message.success('Đã tải ảnh lên; tin đăng cần duyệt lại.');setImages((await api.property(images.id)).data);q.refresh();}).catch(e=>message.error(errorText(e)));
          return false;
        }}><Button type="primary">Tải ảnh từ thiết bị</Button></Upload>
      </Space>
      <List dataSource={images?.images||[]} locale={{emptyText:<Empty description="Chưa có ảnh"/>}} renderItem={(img:PropertyImageDto)=><List.Item actions={[<Button size="small" onClick={async()=>{try{await api.primaryPropertyImage(images!.id,img.id);setImages((await api.property(images!.id)).data);q.refresh();}catch(e){message.error(errorText(e));}}}>Ảnh đại diện</Button>,<Popconfirm title="Xóa ảnh này?" onConfirm={async()=>{try{await api.deletePropertyImage(images!.id,img.id);setImages((await api.property(images!.id)).data);q.refresh();}catch(e){message.error(errorText(e));}}}><Button danger size="small">Xóa</Button></Popconfirm>]}><List.Item.Meta avatar={<Avatar shape="square" src={img.imageUrl}/>} title={img.isPrimary?'Ảnh đại diện':'Ảnh căn nhà'} description={img.imageUrl}/></List.Item>}/></Modal>
  </Page>;
}

export function LandlordBookings() {
  const [page,setPage]=useState(1); const [bookingDetail,setBookingDetail]=useState<BookingDto|null>(null); const [bookingProperty,setBookingProperty]=useState<PropertyDto|null>(null); const [status,setStatus]=useState<string|undefined>(undefined); const [keyword,setKeyword]=useState(''); const q=useRequest(()=>api.incomingBookings({page,pageSize:10,status,keyword:keyword||undefined}),[page,status,keyword]); const {message,modal}=AntApp.useApp();
  const columns:ColumnsType<BookingDto>=[
    {title:'Căn nhà',dataIndex:'propertyTitle'},
    {title:'Người thuê',dataIndex:'tenantName',render:(v,r)=><Space orientation="vertical" size={0}>{v}<Typography.Text type="secondary">{r.tenantEmail}</Typography.Text></Space>},
    {title:'Thời gian thuê',render:(_,r)=>'Từ '+r.startDate+' đến trước '+r.endDate},
    {title:'Giá niêm yết/tháng',dataIndex:'monthlyRent',render:money},
    {title:'Ước tính kỳ thuê',dataIndex:'estimatedRent',render:money},
    {title:'Trạng thái',dataIndex:'status',render:(v:number)=><Status value={v} label={bookingStatusLabels[v] || String(v)}/>},
    {title:'Thao tác',render:(_,r)=><Space><Button size="small" onClick={async()=>{try{const booking=(await api.booking(r.id)).data;setBookingDetail(booking);setBookingProperty((await api.property(booking.propertyId)).data);}catch(e){message.error(errorText(e));}}}>Chi tiết</Button>{r.status===1&&<Button size="small" onClick={async()=>{try{await api.createContract(r.id);message.success('Đã mở hợp đồng. Kiểm tra tiền cọc, điều khoản và gửi khách xác nhận.');window.location.assign('/landlord/contracts');}catch(e){message.error(errorText(e));}}}>Hợp đồng</Button>}{r.status===0&&<><Popconfirm title="Chấp nhận yêu cầu thuê?" onConfirm={async()=>{try{await api.acceptBooking(r.id);setStatus('Accepted');setPage(1);message.success('Đã chấp nhận và tạo hợp đồng. Vào Hợp đồng để kiểm tra và gửi ký.');q.refresh();}catch(e){message.error(errorText(e));}}}><Button type="primary" size="small">Chấp nhận</Button></Popconfirm><Button danger size="small" onClick={()=>modal.confirm({title:'Từ chối yêu cầu',content:<Input.TextArea id="booking-reason" placeholder="Lý do từ chối (không bắt buộc)"/>,onOk:async()=>{try{const reason=(document.getElementById('booking-reason') as HTMLTextAreaElement)?.value||'';await api.rejectBooking(r.id,reason);message.success('Đã từ chối yêu cầu');q.refresh();}catch(e){message.error(errorText(e));throw e;}}})}>Từ chối</Button></>}</Space>}
  ];
  return <Page title="Yêu cầu thuê đến" description="Xem và xử lý yêu cầu của Tenant đối với các căn nhà thuộc sở hữu của bạn."><ErrorLine error={q.error}/><Space className="management-toolbar" wrap style={{marginBottom:16}}><Input.Search value={keyword} onChange={e=>{setPage(1);setKeyword(e.target.value);}} placeholder="Tìm người thuê hoặc căn nhà" style={{width:280}}/><Select allowClear placeholder="Tất cả trạng thái" style={{width:180}} value={status} onChange={v=>{setPage(1);setStatus(v);}} options={['Pending','Accepted','Rejected','Cancelled','Expired'].map(v=>({value:v,label:bookingStatusLabels[({Pending:0,Accepted:1,Rejected:2,Cancelled:3,Expired:4} as Record<string,number>)[v]]}))}/></Space><Card>{q.busy?<Loading/>:<Table rowKey="id" columns={columns} dataSource={q.data?.items||[]} pagination={false} locale={{emptyText:<EmptyState/>}}/>}<Paging page={page} total={q.data?.totalCount||0} onChange={setPage}/></Card><Drawer title="Chi tiết yêu cầu thuê" open={!!bookingDetail} onClose={()=>setBookingDetail(null)} size="large" styles={{ wrapper: { width: 'min(560px, 100vw)' } }}>{bookingDetail&&<>{bookingDetail.status===1&&<Alert type="success" showIcon message="Yêu cầu đã được chấp nhận" description="Hợp đồng được tạo khi chấp nhận. Kiểm tra tiền cọc và điều khoản, sau đó gửi cho khách thuê xác nhận." style={{marginBottom:16}}/>}<Descriptions bordered column={1} items={[{key:'1',label:'Căn nhà',children:bookingDetail.propertyTitle},{key:'2',label:'Người thuê',children:bookingDetail.tenantName},{key:'3',label:'Email',children:bookingDetail.tenantEmail},{key:'4',label:'Ngày bắt đầu',children:dateText(bookingDetail.startDate)},{key:'5',label:'Ngày kết thúc (không tính)',children:dateText(bookingDetail.endDate)},{key:'6',label:'Số người',children:bookingDetail.numberOfPeople},{key:'7',label:'Giá niêm yết/tháng',children:money(bookingDetail.monthlyRent)},{key:'7a',label:'Ước tính tiền thuê kỳ này',children:money(bookingDetail.estimatedRent)},{key:'8',label:'Lời nhắn',children:bookingDetail.message||'—'},{key:'9',label:'Trạng thái',children:<Status value={bookingDetail.status} label={bookingStatusLabels[bookingDetail.status]}/>},{key:'10',label:'Lý do từ chối',children:bookingDetail.rejectionReason||'—'}]}/>
      {bookingProperty&&<><Typography.Title level={5} style={{marginTop:18}}>Thông tin đầy đủ căn nhà</Typography.Title>
        {bookingProperty.images?.length>0&&<Row gutter={[8,8]}>{bookingProperty.images.map(img=><Col xs={12} md={8} key={img.id}><img src={img.imageUrl} alt={bookingProperty.title} style={{width:'100%',height:120,objectFit:'cover',borderRadius:8}}/></Col>)}</Row>}
        <Descriptions bordered column={1} size="small" items={[
          {key:'type',label:'Loại hình',children:bookingProperty.propertyTypeName},
          {key:'address',label:'Địa chỉ',children:[bookingProperty.addressLine,bookingProperty.wardName,bookingProperty.provinceName].filter(Boolean).join(', ')},
          {key:'area',label:'Diện tích',children:bookingProperty.area+' m²'},
          {key:'rooms',label:'Phòng ngủ / phòng tắm',children:bookingProperty.bedrooms+' / '+bookingProperty.bathrooms},
          {key:'capacity',label:'Sức chứa tối đa',children:bookingProperty.maxOccupants+' người'},
          {key:'deposit',label:'Tiền cọc công bố',children:money(bookingProperty.depositAmount)},
          {key:'furniture',label:'Nội thất',children:bookingProperty.furnishingDescription||'Chưa cập nhật'},
          {key:'fees',label:'Điện, nước và phí khác',children:bookingProperty.additionalFeesDescription||'Chưa cập nhật'},
          {key:'desc',label:'Mô tả',children:bookingProperty.description||'Chưa có mô tả'}
        ]}/>
      </>}
      {bookingDetail.status===1&&<Button type="primary" block href="/landlord/contracts" style={{marginTop:16}}>Đi đến quản lý hợp đồng</Button>}</>}</Drawer></Page>;
}

export function LandlordContracts() {
  const [page,setPage]=useState(1); const [status,setStatus]=useState<string|undefined>(); const [keyword,setKeyword]=useState(''); const q=useRequest(()=>api.contracts({page,pageSize:10,status,keyword:keyword||undefined}),[page,status,keyword]); const [edit,setEdit]=useState<ContractDto|null>(null); const [form]=Form.useForm<ContractFormValues>(); const {message}=AntApp.useApp(); const [signingId,setSigningId]=useState<string|null>(null); const [contractDetail,setContractDetail]=useState<ContractDetailDto|null>(null); const [detailBusy,setDetailBusy]=useState(false);
  const openContractDetail=async(id:string)=>{setDetailBusy(true);try{setContractDetail((await api.contract(id)).data);}catch(e){message.error(errorText(e));}finally{setDetailBusy(false);}};
  const columns:ColumnsType<ContractDto>=[
    {title:'Mã hợp đồng',dataIndex:'contractCode'},
    {title:'Căn nhà',dataIndex:'propertyTitle'},
    {title:'Người thuê',dataIndex:'tenantName'},
    {title:'Thời hạn',render:(_,r)=>'Từ '+r.startDate+' đến trước '+r.endDate},
    {title:'Tiền thuê',dataIndex:'monthlyRent',render:money},
    {title:'Ước tính cả kỳ',dataIndex:'estimatedRent',render:money},
    {title:'Trạng thái',dataIndex:'status',render:(v:string,r)=><Space orientation="vertical" size={2}><Status value={v} label={contractStatusLabels[v] || v}/>{v==='AwaitingSignature'&&<Typography.Text type="secondary">{r.landlordSignedAt?'Đã ký · Chờ khách thuê ký':r.tenantSignedAt?'Khách đã ký · Chờ bạn ký':'Chờ hai bên ký'}</Typography.Text>}</Space>},
    {title:'Chữ ký',render:(_,r)=><Space orientation="vertical" size={2}><span>Chủ nhà: {r.landlordSignedAt?'Đã ký':'Chưa ký'}</span><span>Người thuê: {r.tenantSignedAt?'Đã ký':'Chưa ký'}</span></Space>},
    {title:'Thao tác',render:(_,r)=><Space wrap><Button size="small" onClick={()=>void openContractDetail(r.id)}>Xem đầy đủ</Button>{r.status==='Draft'&&<><Button size="small" onClick={()=>{setEdit(r);form.setFieldsValue({...r,terms:r.terms||undefined,startDate:dayjs(r.startDate),endDate:dayjs(r.endDate)});}}>Chỉnh sửa</Button><Popconfirm title="Gửi hợp đồng cho hai bên ký?" onConfirm={async()=>{try{await api.submitContract(r.id);message.success('Đã gửi hợp đồng chờ ký');q.refresh();}catch(e){message.error(errorText(e));}}}><Button size="small" type="primary">Gửi ký</Button></Popconfirm></>}{r.status==='AwaitingSignature'&&!r.landlordSignedAt&&<Popconfirm title="Xác nhận ký hợp đồng?" description="Bạn xác nhận đồng ý với nội dung hợp đồng đã gửi." okText="Xác nhận ký" cancelText="Hủy" onConfirm={async()=>{setSigningId(r.id);try{await api.signContract(r.id);message.success('Đã xác nhận chữ ký. Khi đủ hai chữ ký, hệ thống phát hành hóa đơn ban đầu.');q.refresh();}catch(e){message.error(errorText(e));throw e;}finally{setSigningId(null);}}}><Button type="primary" size="small" loading={signingId===r.id}>Ký hợp đồng</Button></Popconfirm>}</Space>}
  ];
  return <Page title="Quản lý hợp đồng"><ErrorLine error={q.error}/><Space wrap style={{marginBottom:16}}><Input.Search value={keyword} onChange={e=>{setPage(1);setKeyword(e.target.value);}} placeholder="Tìm mã hợp đồng, người thuê hoặc căn nhà" style={{width:320}}/><Select allowClear placeholder="Lọc trạng thái hợp đồng" style={{width:220,marginBottom:16}} value={status} onChange={v=>{setPage(1);setStatus(v);}} options={Object.entries(contractStatusLabels).map(([value,label])=>({value,label}))}/></Space><Card className="management-table-card">{q.busy?<Loading/>:<Table rowKey="id" columns={columns} dataSource={q.data?.items||[]} pagination={false} scroll={{x:1100}} locale={{emptyText:<EmptyState/>}}/>}<Paging page={page} total={q.data?.totalItems||0} onChange={setPage}/></Card><Drawer title="Chỉnh sửa hợp đồng" open={!!edit} onClose={()=>setEdit(null)} size="large" styles={{ wrapper: { width: 'min(500px, 100vw)' } }} extra={<Button type="primary" onClick={()=>form.submit()}>Lưu</Button>}><Alert type="info" showIcon message="Hợp đồng thuê theo tháng" description="Thời hạn dưới một tháng áp dụng mức thuê tối thiểu một tháng. Tháng tròn tính theo giá tháng; ngày lẻ tính theo giá tháng chia 30 nhân số ngày. Tiền cọc được thỏa thuận riêng." style={{marginBottom:16}}/><Form form={form} layout="vertical" onFinish={async (v:ContractFormValues)=>{try{await api.updateContract(edit!.id,{...v,startDate:v.startDate.format('YYYY-MM-DD'),endDate:v.endDate.format('YYYY-MM-DD')});message.success('Đã cập nhật hợp đồng');setEdit(null);q.refresh();}catch(e){message.error(errorText(e));}}}><Form.Item name="startDate" label="Ngày bắt đầu" rules={[{required:true}]}><DatePicker style={{width:'100%'}}/></Form.Item><Form.Item name="endDate" label="Ngày kết thúc (không tính ngày này)" dependencies={['startDate']} rules={[{required:true},{validator:(_,value)=>{const start=form.getFieldValue('startDate');return !start||!value||value.isAfter(start,'day')?Promise.resolve():Promise.reject(new Error('Ngày kết thúc phải sau ngày bắt đầu.'));}}]}><DatePicker style={{width:'100%'}} disabledDate={current=>{const start=form.getFieldValue('startDate');return !!start&&!current.isAfter(start,'day');}}/></Form.Item><Form.Item name="monthlyRent" label="Tiền thuê/tháng" rules={[{required:true},{type:'number',min:0.01,message:'Tiền thuê phải lớn hơn 0.'}]}><InputNumber min={0.01} max={999999999} precision={2} style={{width:'100%'}}/></Form.Item><Form.Item name="depositAmount" label="Tiền cọc" rules={[{required:true},{type:'number',min:0,max:999999999,message:'Tiền cọc không hợp lệ.'}]}><InputNumber min={0} max={999999999} precision={2} style={{width:'100%'}}/></Form.Item><Form.Item name="paymentDueDay" label="Ngày đến hạn hàng tháng" rules={[{required:true}]}><InputNumber min={1} max={28} style={{width:'100%'}}/></Form.Item><Form.Item name="terms" label="Điều khoản"><Input.TextArea rows={5} maxLength={4000} showCount/></Form.Item></Form></Drawer>
    <Drawer title={'Chi tiết đầy đủ hợp đồng '+(contractDetail?.contract.contractCode||'')} open={!!contractDetail} onClose={()=>setContractDetail(null)} size="large" styles={{wrapper:{width:'min(820px, 100vw)'}}}>
      {detailBusy?<Loading/>:contractDetail&&<Space direction="vertical" size={16} style={{width:'100%'}}>
        <Alert type="info" showIcon message="Thông tin hợp đồng và hồ sơ căn nhà" description="Các thông tin dưới đây được lấy từ hợp đồng, tin đăng và yêu cầu thuê đã gửi; thông tin liên hệ chỉ hiển thị cho hai bên tham gia hợp đồng."/>
        <Typography.Title level={5}>Thông tin hợp đồng</Typography.Title>
        <Descriptions bordered size="small" column={1} items={[
          {key:'code',label:'Mã hợp đồng',children:contractDetail.contract.contractCode},
          {key:'status',label:'Trạng thái',children:<Status value={contractDetail.contract.status} label={contractStatusLabels[contractDetail.contract.status]||contractDetail.contract.status}/>},
          {key:'period',label:'Thời hạn thuê',children:'Từ '+dateText(contractDetail.contract.startDate)+' đến trước '+dateText(contractDetail.contract.endDate)},
          {key:'rent',label:'Tiền thuê/tháng',children:money(contractDetail.contract.monthlyRent)},
          {key:'total',label:'Tổng tiền thuê ước tính',children:money(contractDetail.contract.estimatedRent)},
          {key:'deposit',label:'Tiền cọc',children:money(contractDetail.contract.depositAmount)},
          {key:'due',label:'Ngày đến hạn hàng tháng',children:'Ngày '+contractDetail.contract.paymentDueDay},
          {key:'occupants',label:'Số người ở',children:contractDetail.numberOfPeople+' / '+contractDetail.maxOccupants+' người'},
          {key:'terms',label:'Điều khoản bổ sung',children:contractDetail.contract.terms||'Chưa có điều khoản bổ sung.'},
          {key:'landlordSign',label:'Chữ ký chủ nhà',children:contractDetail.contract.landlordSignedAt?dateText(contractDetail.contract.landlordSignedAt):'Chưa ký'},
          {key:'tenantSign',label:'Chữ ký người thuê',children:contractDetail.contract.tenantSignedAt?dateText(contractDetail.contract.tenantSignedAt):'Chưa ký'},
          {key:'signed',label:'Có hiệu lực lúc',children:contractDetail.contract.signedAt?dateText(contractDetail.contract.signedAt):'Chưa có hiệu lực'}
        ]}/>
        <Typography.Title level={5}>Thông tin căn nhà</Typography.Title>
        {contractDetail.images.length>0&&<Row gutter={[8,8]}>{contractDetail.images.map(img=><Col xs={12} md={8} key={img.id}><img src={img.imageUrl} alt={contractDetail.propertyTitle} style={{width:'100%',height:140,objectFit:'cover',borderRadius:8}}/></Col>)}</Row>}
        <Descriptions bordered size="small" column={1} items={[
          {key:'title',label:'Tên căn nhà',children:contractDetail.propertyTitle},
          {key:'type',label:'Loại hình',children:contractDetail.propertyTypeName},
          {key:'address',label:'Địa chỉ',children:[contractDetail.addressLine,contractDetail.wardName,contractDetail.provinceName].filter(Boolean).join(', ')},
          {key:'area',label:'Diện tích',children:contractDetail.area+' m²'},
          {key:'rooms',label:'Phòng ngủ / phòng tắm',children:contractDetail.bedrooms+' / '+contractDetail.bathrooms},
          {key:'capacity',label:'Sức chứa tối đa',children:contractDetail.maxOccupants+' người'},
          {key:'furniture',label:'Nội thất',children:contractDetail.furnishingDescription||'Chưa cập nhật'},
          {key:'amenities',label:'Tiện nghi',children:contractDetail.amenities.length?contractDetail.amenities.join(', '):'Chưa cập nhật'},
          {key:'fees',label:'Điện, nước và phí khác',children:contractDetail.additionalFeesDescription||'Theo thỏa thuận trong hợp đồng'},
          {key:'description',label:'Mô tả',children:contractDetail.propertyDescription||'Chưa có mô tả'}
        ]}/>
        <Typography.Title level={5}>Thông tin hai bên</Typography.Title>
        <Descriptions bordered size="small" column={1} items={[
          {key:'ln',label:'Chủ nhà',children:contractDetail.landlordName},
          {key:'le',label:'Email chủ nhà',children:contractDetail.landlordEmail||'Chưa cung cấp'},
          {key:'lp',label:'Điện thoại chủ nhà',children:contractDetail.landlordPhone||'Chưa cung cấp'},
          {key:'tn',label:'Người thuê',children:contractDetail.tenantName},
          {key:'te',label:'Email người thuê',children:contractDetail.tenantEmail||'Chưa cung cấp'},
          {key:'tp',label:'Điện thoại người thuê',children:contractDetail.tenantPhone||'Chưa cung cấp'}
        ]}/>
        <Typography.Title level={5}>Yêu cầu thuê ban đầu</Typography.Title>
        <Descriptions bordered size="small" column={1} items={[
          {key:'people',label:'Số người đăng ký',children:contractDetail.numberOfPeople},
          {key:'message',label:'Lời nhắn của người thuê',children:contractDetail.bookingMessage||'Không có lời nhắn'}
        ]}/>
      </Space>}
    </Drawer></Page>;
}

export function LandlordPayments() {
  const [page,setPage]=useState(1);
  const [status,setStatus]=useState<string|undefined>('Pending');
  const [keyword,setKeyword]=useState('');
  const q=useRequest(()=>api.incomingPayments({page,pageSize:10,status,keyword:keyword||undefined}),[page,status,keyword]);
  const {message,modal}=AntApp.useApp();
  const [busyId,setBusyId]=useState<string|null>(null);
  const refresh=()=>q.refresh();
  const columns:ColumnsType<AdminPaymentListDto>=[
    {title:'Người thanh toán',dataIndex:'payerName'},
    {title:'Căn nhà',dataIndex:'propertyTitle'},
    {title:'Hóa đơn',dataIndex:'invoiceCode'},
    {title:'Phương thức',render:(_,r)=><Space direction="vertical" size={0}><span>{r.paymentMethod==='BankTransfer'?'Chuyển khoản':r.paymentMethod==='Cash'?'Tiền mặt':r.paymentMethod}</span><Typography.Text type="secondary">{r.paymentProvider||'—'}</Typography.Text></Space>},
    {title:'Mã giao dịch',dataIndex:'transactionCode',render:(v:string|null)=><Typography.Text copyable={!!v}>{v||'Chưa có'}</Typography.Text>},
    {title:'Số tiền',dataIndex:'amount',render:money},
    {title:'Trạng thái',dataIndex:'status',render:(v:string)=><Status value={v} label={paymentStatusLabels[v]||v}/>},
    {title:'Thao tác',render:(_,r)=><Space>{r.status==='Pending'&&r.paymentProvider==='Manual'&&r.paymentMethod==='Cash'&&<>
      <Popconfirm title="Xác nhận đã nhận khoản tiền này?" description="Chỉ xác nhận sau khi đã kiểm tra tiền thực nhận." okText="Đã nhận tiền" onConfirm={async()=>{setBusyId(r.id);try{await api.confirmPayment(r.id);message.success('Đã xác nhận thanh toán');refresh();}catch(e){message.error(errorText(e));}finally{setBusyId(null);}}}>
        <Button size="small" type="primary" loading={busyId===r.id}>Xác nhận</Button>
      </Popconfirm>
      <Button size="small" danger onClick={()=>modal.confirm({title:'Từ chối giao dịch',content:<Input.TextArea id={'reject-payment-'+r.id} rows={3} maxLength={1000} placeholder="Nhập lý do từ chối"/>,onOk:async()=>{const reason=(document.getElementById('reject-payment-'+r.id) as HTMLTextAreaElement)?.value||'';if(!reason.trim()){message.error('Vui lòng nhập lý do.');throw new Error('Thiếu lý do');}setBusyId(r.id);try{await api.rejectPayment(r.id,reason);message.success('Đã từ chối giao dịch');refresh();}catch(e){message.error(errorText(e));throw e;}finally{setBusyId(null);}}})}>Từ chối</Button>
    </>}</Space>}
  ];
  return <Page title="Quản lý thanh toán" description="Theo dõi giao dịch SePay tự động và xác nhận các khoản tiền mặt khách báo đã thanh toán.">
    <ErrorLine error={q.error}/>
    <Space wrap style={{marginBottom:16}}>
      <Input.Search value={keyword} onChange={e=>{setPage(1);setKeyword(e.target.value);}} placeholder="Tìm khách, hóa đơn, căn nhà hoặc mã giao dịch" style={{width:320}}/>
      <Select value={status} allowClear placeholder="Tất cả trạng thái" style={{width:190}} onChange={v=>{setPage(1);setStatus(v);}} options={Object.entries(paymentStatusLabels).map(([value,label])=>({value,label}))}/>
    </Space>
    <Card className="management-table-card">
      {q.busy?<Loading/>:<Table rowKey="id" columns={columns} dataSource={q.data?.items||[]} pagination={false} scroll={{x:1250}} locale={{emptyText:<EmptyState/>}}/>}
      <Paging page={page} total={q.data?.totalItems||0} onChange={setPage}/>
    </Card>
    <Alert style={{marginTop:16}} type="info" showIcon message="Nguyên tắc xác nhận" description="Chỉ xác nhận khoản tiền mặt sau khi đã trực tiếp nhận đủ tiền. Thanh toán SePay được hệ thống xác nhận tự động qua IPN/Webhook hợp lệ; không cần chủ nhà xác nhận thủ công."/>
  </Page>;
}

export function LandlordInvoices() {
  const [page,setPage]=useState(1); const [status,setStatus]=useState<string|undefined>(); const [type,setType]=useState<string|undefined>(); const [keyword,setKeyword]=useState(''); const q=useRequest(()=>api.invoices({page,pageSize:10,status,type,keyword:keyword||undefined}),[page,status,type,keyword]); const [detail,setDetail]=useState<InvoiceDetailDto|null>(null); const [contract,setContract]=useState(''); const [form]=Form.useForm<InvoiceFormValues>(); const {message,modal}=AntApp.useApp();
  const columns:ColumnsType<InvoiceListDto>=[
    {title:'Hóa đơn',dataIndex:'invoiceCode'}, {title:'Căn nhà',dataIndex:'propertyTitle'}, {title:'Loại',dataIndex:'invoiceType',render:(v:string)=><span>{invoiceTypeLabels[v] || v}</span>}, {title:'Kỳ thuê',render:(_:unknown,r:InvoiceListDto)=>r.periodStart&&r.periodEnd?dateText(r.periodStart)+' – trước '+dateText(r.periodEnd):'—'}, {title:'Hạn thanh toán',dataIndex:'dueDate',render:(v:string,r:InvoiceListDto)=><Space orientation="vertical" size={2}><span>{dateText(v)}</span>{invoiceDueHint(v,r.status)}</Space>}, {title:'Tổng tiền',dataIndex:'totalAmount',render:money}, {title:'Trạng thái',dataIndex:'status',render:(v:string)=><Status value={v} label={invoiceStatusLabels[v] || v}/>},
    {title:'Thao tác',render:(_,r)=><Button size="small" onClick={async()=>{try{setDetail((await api.invoice(r.id)).data);}catch(e){message.error(errorText(e));}}}>Chi tiết & thanh toán</Button>}
  ];
  return <Page title="Hóa đơn và thanh toán"><ErrorLine error={q.error}/><Space wrap style={{marginBottom:16}}><Input.Search value={keyword} onChange={e=>{setPage(1);setKeyword(e.target.value);}} placeholder="Tìm mã hóa đơn, hợp đồng hoặc căn nhà" style={{width:300}}/><Select allowClear placeholder="Lọc trạng thái" style={{width:180}} value={status} onChange={v=>{setPage(1);setStatus(v);}} options={Object.entries(invoiceStatusLabels).map(([value,label])=>({value,label}))}/><Select allowClear placeholder="Lọc loại hóa đơn" style={{width:210}} value={type} onChange={v=>{setPage(1);setType(v);}} options={Object.entries(invoiceTypeLabels).map(([value,label])=>({value,label}))}/><Input value={contract} onChange={e=>setContract(e.target.value)} placeholder="Contract ID (UUID)" style={{width:260}}/><Button type="primary" onClick={()=>{form.resetFields();form.setFieldsValue({invoiceType:1,issueDate:dayjs().format('YYYY-MM-DD')});modal.confirm({title:'Tạo hóa đơn cho hợp đồng',content:<Form form={form} layout="vertical"><Form.Item name="invoiceType" label="Loại hóa đơn" rules={[{required:true}]}><Select options={[{value:0,label:'Tiền cọc'},{value:1,label:'Tiền thuê hàng tháng'},{value:2,label:'Khác'}]}/></Form.Item><Form.Item name="issueDate" label="Ngày phát hành (YYYY-MM-DD)" rules={[{required:true},{pattern:/^\d{4}-\d{2}-\d{2}$/,message:'Ngày phải theo định dạng YYYY-MM-DD.'}]}><Input placeholder="2026-11-25"/></Form.Item><Form.Item name="dueDate" label="Hạn thanh toán (YYYY-MM-DD)" dependencies={['issueDate']} rules={[{required:true},{pattern:/^\d{4}-\d{2}-\d{2}$/,message:'Ngày phải theo định dạng YYYY-MM-DD.'},{validator:(_,v)=>{const issue=form.getFieldValue('issueDate');return !issue||!v||Date.parse(v)>=Date.parse(issue)?Promise.resolve():Promise.reject(new Error('Hạn thanh toán không được trước ngày phát hành.'));}}]}><Input placeholder="2026-12-05"/></Form.Item><Form.Item name="periodStart" label="Ngày bắt đầu kỳ thuê" dependencies={['invoiceType']} rules={[{validator:(_,v)=>{const required=form.getFieldValue('invoiceType')===1;const end=form.getFieldValue('periodEnd');if(!v)return required||end?Promise.reject(new Error('Vui lòng nhập ngày bắt đầu kỳ.')):Promise.resolve();return /^\d{4}-\d{2}-\d{2}$/.test(v)?Promise.resolve():Promise.reject(new Error('Ngày phải theo định dạng YYYY-MM-DD.'));}}]}><Input placeholder="2026-12-01"/></Form.Item><Form.Item name="periodEnd" label="Ngày kết thúc kỳ thuê (không tính ngày này)" dependencies={['invoiceType','periodStart']} rules={[{validator:(_,v)=>{const required=form.getFieldValue('invoiceType')===1;const start=form.getFieldValue('periodStart');if(!v)return required||start?Promise.reject(new Error('Vui lòng nhập ngày kết thúc kỳ.')):Promise.resolve();return /^\d{4}-\d{2}-\d{2}$/.test(v)&&!!start&&Date.parse(v)>Date.parse(start)?Promise.resolve():Promise.reject(new Error('Ngày kết thúc kỳ phải hợp lệ và sau ngày bắt đầu.'));}}]}><Input placeholder="2026-12-31"/></Form.Item><Form.Item noStyle shouldUpdate={(previous,current)=>previous.invoiceType!==current.invoiceType}>{({getFieldValue})=>getFieldValue('invoiceType')===1?<Alert type="info" showIcon message="Tiền thuê được hệ thống tự tính" description="Hệ thống lấy đơn giá từ hợp đồng. Tháng tròn tính theo giá tháng; ngày lẻ tính bằng giá tháng chia 30 nhân số ngày. Phí điện, nước và khoản phát sinh lập hóa đơn loại Khác."/>:<><Form.Item name="description" label="Nội dung dòng hóa đơn" rules={[{required:true},{max:300}]}><Input placeholder="Ví dụ: Tiền điện, tiền nước..."/></Form.Item><Form.Item name="quantity" label="Số lượng" rules={[{required:true,type:'number',min:0.01}]}><InputNumber min={0.01} max={1000000} style={{width:'100%'}}/></Form.Item><Form.Item name="unitPrice" label="Đơn giá" rules={[{required:true,type:'number',min:0.01}]}><InputNumber min={0.01} max={999999999} style={{width:'100%'}}/></Form.Item></>}</Form.Item></Form>,onOk:async()=>{try{const v=await form.validateFields();if(!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(contract.trim()))throw new Error('Contract ID phải là UUID hợp lệ.');const payload:CreateInvoiceRequest={invoiceType:v.invoiceType,issueDate:v.issueDate,dueDate:v.dueDate,periodStart:v.periodStart||null,periodEnd:v.periodEnd||null,items:v.invoiceType===1?[]:[{description:v.description,quantity:v.quantity,unitPrice:v.unitPrice}]};await api.createInvoice(contract.trim(),payload);message.success('Đã tạo hóa đơn');q.refresh();}catch(e){message.error(errorText(e));throw e;}}});}}>Tạo hóa đơn</Button></Space><Card>{q.busy?<Loading/>:<Table rowKey="id" columns={columns} dataSource={q.data?.items||[]} pagination={false}/>}<Paging page={page} total={q.data?.totalItems||0} onChange={setPage}/></Card><Drawer title={'Hóa đơn '+(detail?.invoiceCode||'')} open={!!detail} onClose={()=>setDetail(null)} size="large" styles={{ wrapper: { width: 'min(620px, 100vw)' } }}>{detail&&<><Descriptions bordered column={1} items={[{key:'a',label:'Căn nhà',children:detail.propertyTitle},{key:'period',label:'Kỳ thuê',children:detail.periodStart&&detail.periodEnd?dateText(detail.periodStart)+' – trước '+dateText(detail.periodEnd):'—'},{key:'due',label:'Hạn thanh toán',children:<Space>{dateText(detail.dueDate)}{invoiceDueHint(detail.dueDate,detail.status)}</Space>},{key:'b',label:'Tổng tiền',children:money(detail.totalAmount)},{key:'c',label:'Trạng thái',children:<Status value={detail.status} label={invoiceStatusLabels[detail.status]||detail.status}/>}]}/><Typography.Title level={5}>Chi tiết dòng hóa đơn</Typography.Title><List dataSource={detail.items} locale={{emptyText:<Empty description="Không có dòng chi tiết"/>}} renderItem={item=><List.Item><List.Item.Meta title={item.description} description={'Số lượng: '+item.quantity+' · Đơn giá: '+money(item.unitPrice)}/>{money(item.amount)}</List.Item>}/><Typography.Title level={5}>Các khoản thanh toán</Typography.Title><List dataSource={detail.payments||[]} locale={{emptyText:<Empty description="Chưa có thanh toán"/>}} renderItem={(p:PaymentDto)=><List.Item actions={p.paymentProvider==='Manual'&&p.status==='Pending'?[<Button size="small" type="primary" onClick={async()=>{try{await api.confirmPayment(p.id);setDetail((await api.invoice(detail.id)).data);message.success('Đã xác nhận thanh toán');}catch(e){message.error(errorText(e));}}}>Xác nhận</Button>,<Button danger size="small" onClick={()=>modal.confirm({title:'Từ chối thanh toán thủ công',content:<Input.TextArea id={'payment-reason-'+p.id} placeholder="Lý do từ chối" rows={3} maxLength={1000}/>,onOk:async()=>{const reason=(document.getElementById('payment-reason-'+p.id) as HTMLTextAreaElement)?.value||'';if(!reason.trim()){message.error('Vui lòng nhập lý do từ chối.');throw new Error('Thiếu lý do');}try{await api.rejectPayment(p.id,reason);setDetail((await api.invoice(detail.id)).data);message.success('Đã từ chối');}catch(e){message.error(errorText(e));throw e;}}})}>Từ chối</Button>]:[]}><List.Item.Meta title={<Space>{p.paymentMethod==='Cash'?'Tiền mặt':p.paymentMethod==='BankTransfer'?'Chuyển khoản':p.paymentMethod}<Status value={p.status} label={paymentStatusLabels[p.status]||p.status}/>{p.paymentProvider==='SePay'&&<Tag color="blue">SePay · IPN</Tag>}</Space>} description={p.transactionCode||p.failureReason||p.paymentProvider||'—'}/>{money(p.amount)}</List.Item>}/></>}{detail?.payments.some(p=>p.paymentProvider==='SePay')&&<Alert style={{marginTop:12}} type="info" showIcon message="Thanh toán SePay được cập nhật qua IPN/Bank Webhook, không xác nhận thủ công."/>}</Drawer></Page>;
}

export function LandlordNotifications() {
  const q=useRequest(()=>api.notifications({page:1,pageSize:30}),[]);
  const unread=useRequest(()=>api.unreadCount(),[]);
  const {message}=AntApp.useApp();
  const [markingAll,setMarkingAll]=useState(false);
  const [markingId,setMarkingId]=useState<string|null>(null);
  const items=q.data?.items||[];

  const markAllRead=async()=>{
    setMarkingAll(true);
    try{
      await api.readAllNotifications();
      q.refresh();
      unread.refresh();
      window.dispatchEvent(new Event('web2:notifications-updated'));
      message.success('Đã đánh dấu tất cả thông báo là đã đọc.');
    }catch(e){message.error(errorText(e));}
    finally{setMarkingAll(false);}
  };

  const markRead=async(id:string)=>{
    setMarkingId(id);
    try{
      await api.readNotification(id);
      q.refresh();
      unread.refresh();
      window.dispatchEvent(new Event('web2:notifications-updated'));
      message.success('Đã đánh dấu thông báo là đã đọc.');
    }catch(e){message.error(errorText(e));}
    finally{setMarkingId(null);}
  };

  return (
    <Page title="Thông báo" description="Cập nhật về tin đăng, yêu cầu thuê, hợp đồng và thanh toán.">
      <ErrorLine error={q.error||unread.error}/>
      <div className="tenant-page-toolbar">
        <div className="tenant-unread-summary">
          <span className="tenant-unread-icon"><BellOutlined/></span>
          <div>
            <Typography.Text strong>{unread.data?.unreadCount||0} thông báo chưa đọc</Typography.Text>
            <Typography.Text type="secondary">Trên toàn bộ tài khoản</Typography.Text>
          </div>
        </div>
        <Button loading={markingAll} disabled={!unread.data?.unreadCount||q.busy||!!q.error} onClick={()=>void markAllRead()}>
          Đánh dấu tất cả đã đọc
        </Button>
      </div>
      <Card className="tenant-list-card" bordered={false}>
        {q.busy?<Loading/>:<List
          dataSource={items}
          locale={{emptyText:<EmptyState description="Bạn chưa có thông báo nào."/>}}
          renderItem={(n:NotificationDto)=>(
            <List.Item className={'tenant-list-item tenant-notification-item'+(!n.isRead?' is-unread':'')}
              actions={!n.isRead?[<Button key="read" type="text" loading={markingId===n.id} onClick={()=>void markRead(n.id)}>Đánh dấu đã đọc</Button>]:[]}>
              <List.Item.Meta
                title={<Space wrap>{n.title}{!n.isRead&&<Badge status="processing" text="Mới"/>}</Space>}
                description={<div className="tenant-notification-content"><Typography.Paragraph>{n.content}</Typography.Paragraph><Typography.Text type="secondary">{dateText(n.createdAt)}</Typography.Text></div>}
              />
            </List.Item>
          )}
        />}
      </Card>
    </Page>
  );
}

export function LandlordChat() {
  const [page,setPage]=useState(1);
  const [includeArchived,setIncludeArchived]=useState(false);
  const q=useRequest(()=>api.conversations({page,pageSize:30,includeArchived}),[page,includeArchived]);
  const [active,setActive]=useState<ConversationDto|null>(null);
  const [messages,setMessages]=useState<ChatMessageDto[]>([]);
  const [text,setText]=useState('');
  const [archiving,setArchiving]=useState(false);
  const refreshConversations=useRef(q.refresh);
  const { message } = AntApp.useApp();
  useEffect(()=>{refreshConversations.current=q.refresh;},[q.refresh]);
  useEffect(()=>{setPage(1);setActive(null);setMessages([]);},[includeArchived]);
  useEffect(()=>{
    if(!active)return;
    let mounted=true;
    const conn=new HubConnectionBuilder().withUrl((import.meta.env.VITE_API_BASE_URL||location.origin)+'/hubs/chat',{accessTokenFactory:()=>sessionStorage.getItem(TOKEN_KEY)||''}).withAutomaticReconnect().configureLogging(LogLevel.Warning).build();
    conn.on('NhanTinNhan',(payload:ChatMessageDto & {attachment?:ChatAttachmentDto})=>{
      const m:ChatMessageDto={...payload,attachments:payload.attachments??(payload.attachment?[payload.attachment]:[])};
      if(mounted)setMessages(old=>old.some(x=>x.messageId===m.messageId)?old:[...old,m]);
      refreshConversations.current();
    });
    void conn.start().then(()=>conn.invoke('JoinConversation',active.conversationId)).catch(()=>{if(mounted)message.error('Không thể kết nối chat realtime. Vui lòng tải lại hội thoại.');});
    return()=>{mounted=false;void conn.stop();};
  },[active?.conversationId]);
  const select=async(c:ConversationDto)=>{
    setActive(c);
    try{const r=await api.messages(c.conversationId);setMessages(r.data.items||[]);await api.markChatRead(c.conversationId);refreshConversations.current();}
    catch(e){message.error(errorText(e));}
  };
  const archive=async()=>{
    if(!active)return;
    setArchiving(true);
    try{
      await api.archiveConversation(active.conversationId,!active.isArchived);
      message.success(active.isArchived?'Đã khôi phục hội thoại':'Đã lưu trữ hội thoại');
      setActive(null);setMessages([]);q.refresh();
    }catch(e){message.error(errorText(e));}
    finally{setArchiving(false);}
  };
  const send=async()=>{
    const content=text.trim();
    if(!content){message.warning('Vui lòng nhập nội dung tin nhắn.');return;}
    if(content.length>4000){message.error('Tin nhắn không được vượt quá 4000 ký tự.');return;}
    try{
      await api.sendMessage(active!.conversationId,content);
      setText('');
      if(active?.isArchived){setActive(old=>old?{...old,isArchived:false}:old);setIncludeArchived(false);setPage(1);}
      q.refresh();
    }catch(e){message.error(errorText(e));}
  };
  const items=(q.data?.items||[]).filter(c=>includeArchived?c.isArchived:!c.isArchived);
  return <Page title="Trao đổi với người thuê"><ErrorLine error={q.error}/><div className="chat"><aside><div className="chat-inbox-toolbar"><Segmented block value={includeArchived?'archived':'active'} onChange={value=>setIncludeArchived(value==='archived')} options={[{value:'active',label:'Đang hoạt động'},{value:'archived',label:'Đã lưu trữ'}]}/></div><List dataSource={items} loading={q.busy} locale={{emptyText:<Empty description={includeArchived?'Chưa có hội thoại đã lưu trữ':'Chưa có hội thoại đang hoạt động'}/>}} renderItem={(c:ConversationDto)=><List.Item onClick={()=>void select(c)} style={{cursor:'pointer'}}><List.Item.Meta title={c.propertyTitle||c.tenantName||'Cuộc hội thoại'} description={c.lastMessageContent||'Mở cuộc trò chuyện'}/>{c.unreadCount>0&&<Badge count={c.unreadCount}/>}</List.Item>}/><Paging page={page} total={q.data?.totalItems||0} onChange={setPage}/></aside><section>{!active?<Empty description="Chọn một hội thoại để xem tin nhắn"/>:<><div style={{display:'flex',justifyContent:'space-between',alignItems:'center',marginBottom:12}}><Typography.Text strong>{active.propertyTitle||active.tenantName}</Typography.Text><Button size="small" loading={archiving} onClick={()=>void archive()}>{active.isArchived?'Đưa về hộp thư đến':'Lưu trữ'}</Button></div><div className="messages">{messages.length===0&&<Empty description="Chưa có tin nhắn"/>}{messages.map(m=><div key={m.messageId} className={m.isMine?'bubble mine':'bubble'}><small>{m.senderName}</small><p>{m.content}</p>{m.attachments.map(a=><Button key={a.attachmentId} type="link" onClick={async()=>{try{const response=await api.downloadAttachment(a.attachmentId);const url=URL.createObjectURL(response.data);const link=document.createElement('a');link.href=url;link.download=a.fileName;document.body.appendChild(link);link.click();link.remove();URL.revokeObjectURL(url);}catch(e){message.error(errorText(e));}}}>{a.fileName}</Button>)}</div>)}</div>{active.isArchived&&<Alert type="info" showIcon message="Hội thoại đã lưu trữ" description="Khi gửi tin nhắn hoặc tệp mới, hội thoại sẽ tự chuyển về Hộp thư đến." style={{marginBottom:8}}/>}<Space.Compact className="composer"><Input value={text} maxLength={4000} onChange={e=>setText(e.target.value)} onPressEnter={()=>void send()}/><Button type="primary" onClick={()=>void send()} disabled={!text.trim()}>Gửi</Button><Upload showUploadList={false} beforeUpload={file=>{const ext=file.name.split('.').pop()?.toLowerCase()||'';const allowed=['jpg','jpeg','png','webp','pdf','doc','docx'];if(file.size>10*1024*1024){message.error('Tệp đính kèm không được vượt quá 10 MB.');return Upload.LIST_IGNORE;}if(!allowed.includes(ext)){message.error('Chỉ hỗ trợ JPG, JPEG, PNG, WEBP, PDF, DOC và DOCX.');return Upload.LIST_IGNORE;}void api.uploadAttachment(active.conversationId,file).then(()=>{setActive(old=>old?{...old,isArchived:false}:old);setIncludeArchived(false);setPage(1);message.success('Đã gửi tệp');q.refresh();}).catch(e=>message.error(errorText(e)));return false;}}><Button>Tệp</Button></Upload></Space.Compact></>}</section></div></Page>;
}
