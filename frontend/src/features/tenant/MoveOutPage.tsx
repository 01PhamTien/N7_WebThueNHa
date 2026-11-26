import {useEffect,useState} from 'react';
import {Alert,Button,Card,DatePicker,Descriptions,InputNumber,Empty,Form,Input,Modal,Select,Space,Table,Tag,Typography,message} from 'antd';
import dayjs from 'dayjs';
import {Page} from '../../components/Shared';
import {tenantApi} from '../../services/tenant';
import {api} from '../../services/api';
import {errorText} from '../../services/api';
import type {ColumnsType} from 'antd/es/table';
type MoveOut={handoverAt?:string;inspectionNotes?:string;rentDue?:number;rentPaid?:number;otherDue?:number;otherPaid?:number;refundAmount?:number;additionalAmount?:number;electricReading?:number;waterReading?:number;depositPaid?:number;outstanding?:number;extraCharges?:number;settlementNotes?:string;landlordConfirmedAt?:string;tenantConfirmedAt?:string;transferReference?:string;id:string;contractId:string;contractCode:string;propertyTitle:string;requestedMoveOutDate:string;reason:string;tenantNotes?:string;landlordNotes?:string;status:string;createdAt:string;tenantName:string;landlordName:string};
const labels:Record<string,string>={Pending:'Chờ chủ nhà phản hồi',Approved:'Đã chấp nhận',Rejected:'Đã từ chối',Cancelled:'Đã hủy',InspectionScheduled:'Đã hẹn bàn giao',AwaitingSettlement:'Chờ quyết toán',Completed:'Đã hoàn tất'};
const colors:Record<string,string>={Pending:'gold',Approved:'blue',Rejected:'red',Cancelled:'default',Completed:'green',InspectionScheduled:'processing',AwaitingSettlement:'orange'};
export function MoveOutPage({landlord=false}:{landlord?:boolean}){
 const [rows,setRows]=useState<MoveOut[]>([]);const [contracts,setContracts]=useState<any[]>([]);const [loading,setLoading]=useState(false);const [open,setOpen]=useState(false);const [form]=Form.useForm();const [busy,setBusy]=useState(false);
 const load=async()=>{setLoading(true);try{const r=await api.get<MoveOut[]>('/api/move-outs');setRows(r.data);if(!landlord){const c=await tenantApi.contracts(1,50);setContracts(c.data.items.filter((x:any)=>x.status==='Active'));}}catch(e){message.error(errorText(e));}finally{setLoading(false);}};
 useEffect(()=>{void load();},[]);
 const submit=async()=>{try{const v=await form.validateFields();setBusy(true);await tenantApi.createMoveOut({contractId:v.contractId,requestedMoveOutDate:v.date.format('YYYY-MM-DD'),reason:v.reason,tenantNotes:v.notes});message.success('Đã gửi yêu cầu trả nhà');setOpen(false);form.resetFields();await load();}catch(e){if(e instanceof Error&&e.message)message.error(errorText(e));}finally{setBusy(false);}};
 const respond=async(id:string,approve:boolean)=>{try{await api.post('/api/move-outs/'+id+'/landlord-response',{approve});message.success(approve?'Đã chấp nhận yêu cầu':'Đã từ chối yêu cầu');await load();}catch(e){message.error(errorText(e));}};
 const cancel=async(id:string)=>{try{await tenantApi.cancelMoveOut(id);message.success('Đã hủy yêu cầu');await load();}catch(e){message.error(errorText(e));}};
 const [detail,setDetail]=useState<MoveOut>(); const [step,setStep]=useState<string>(); const [stepForm]=Form.useForm();
 const selected=rows.find(x=>x.id===detail?.id);
 const runStep=async()=>{try{const v=await stepForm.validateFields(); if(!selected||!step)return;setBusy(true);await api.post('/api/move-outs/'+selected.id+'/'+step,step==='schedule'?{handoverAt:v.date.toISOString()}:step==='settlement'?{...v,extraCharges:v.extraCharges??0}:step==='confirm'?{reference:v.reference??null}:{});message.success('Đã cập nhật');setStep(undefined);await load();}catch(e){message.error(errorText(e));}finally{setBusy(false);}};
 const begin=(s:string)=>{stepForm.resetFields();setStep(s);};
 const cash=(n?:number)=>(n??0).toLocaleString('vi-VN')+' đ';
 const columns:ColumnsType<MoveOut>=[
  {title:'Căn nhà',dataIndex:'propertyTitle'},
  {title:landlord?'Người thuê':'Mã hợp đồng',dataIndex:landlord?'tenantName':'contractCode'},
  {title:'Ngày dự kiến trả',dataIndex:'requestedMoveOutDate',render:v=>dayjs(v).format('DD/MM/YYYY')},
  {title:'Trạng thái',dataIndex:'status',render:v=><Tag color={colors[v]||'default'}>{labels[v]||v}</Tag>},
  {title:'Lý do',dataIndex:'reason',ellipsis:true},
  {title:'Thao tác',render:(_,r)=><Space><Button size="small" onClick={()=>setDetail(r)}>Chi tiết / Bàn giao</Button>{landlord&&r.status==='Pending'&&<><Button size="small" type="primary" onClick={()=>void respond(r.id,true)}>Chấp nhận</Button><Button size="small" danger onClick={()=>void respond(r.id,false)}>Từ chối</Button></>}{!landlord&&['Pending','Approved'].includes(r.status)&&<Button size="small" danger onClick={()=>void cancel(r.id)}>Hủy yêu cầu</Button>}</Space>}
 ];
 return <Page title={landlord?'Quản lý trả nhà':'Yêu cầu trả nhà'} description={landlord?'Tiếp nhận yêu cầu và thống nhất lịch trả nhà với người thuê.':'Gửi yêu cầu trả nhà và theo dõi phản hồi từ chủ nhà.'}>
  {!landlord&&<Alert type="info" showIcon style={{marginBottom:16}} message="Gửi yêu cầu không tự động kết thúc hợp đồng." description="Hợp đồng chỉ được xem xét kết thúc sau khi hai bên hoàn tất bàn giao và đối soát tiền thuê, phí phát sinh, công nợ và tiền cọc."/>}
  <Card title={landlord?'Yêu cầu trả nhà đến':'Các yêu cầu trả nhà của tôi'} extra={!landlord&&<Button type="primary" onClick={()=>setOpen(true)}>Tạo yêu cầu trả nhà</Button>}>
   <Table rowKey="id" loading={loading} columns={columns} dataSource={rows} pagination={{pageSize:10}} locale={{emptyText:<Empty description="Chưa có yêu cầu trả nhà"/>}}/>
  </Card>
  <Modal title="Gửi yêu cầu trả nhà" open={open} onCancel={()=>setOpen(false)} onOk={()=>void submit()} confirmLoading={busy} okText="Gửi yêu cầu">
   <Form form={form} layout="vertical">
    <Form.Item name="contractId" label="Hợp đồng đang hiệu lực" rules={[{required:true,message:'Chọn hợp đồng'}]}><Select options={contracts.map(x=>({value:x.id,label:x.contractCode+' · '+x.propertyTitle}))} placeholder="Chọn hợp đồng"/></Form.Item>
    <Form.Item name="date" label="Ngày dự kiến trả nhà" rules={[{required:true,message:'Chọn ngày trả nhà'}]}><DatePicker style={{width:'100%'}} minDate={dayjs()} format="DD/MM/YYYY"/></Form.Item>
    <Form.Item name="reason" label="Lý do trả nhà" rules={[{required:true,min:3,max:1000}]}><Input.TextArea rows={3} maxLength={1000}/></Form.Item>
    <Form.Item name="notes" label="Ghi chú thêm"><Input.TextArea rows={2} maxLength={2000}/></Form.Item>
   </Form>
  </Modal>
  <Modal title="Chi tiết bàn giao" open={!!selected} onCancel={()=>setDetail(undefined)} footer={null} width={720}>{selected&&<Space direction="vertical" style={{width:'100%'}}>
   <Descriptions bordered column={1} items={[{key:'c',label:'Hợp đồng',children:selected.contractCode},{key:'h',label:'Lịch bàn giao',children:selected.handoverAt?dayjs(selected.handoverAt.endsWith('Z')?selected.handoverAt:selected.handoverAt+'Z').format('DD/MM/YYYY HH:mm'):'Chưa hẹn'},{key:'i',label:'Biên bản hiện trạng',children:selected.inspectionNotes||'—'},{key:'e',label:'Điện / nước',children:(selected.electricReading??'—')+' / '+(selected.waterReading??'—')},{key:'d',label:'Cọc đã đóng',children:cash(selected.depositPaid)},{key:'rd',label:'Tiền thuê thực tế',children:cash(selected.rentDue)},{key:'rp',label:'Tiền thuê đã đóng',children:cash(selected.rentPaid)},{key:'od',label:'Hóa đơn phí khác',children:cash(selected.otherDue)},{key:'op',label:'Phí khác đã đóng',children:cash(selected.otherPaid)},{key:'x',label:'Phí phát sinh',children:cash(selected.extraCharges)},{key:'r',label:'Hoàn khách',children:cash(selected.refundAmount)},{key:'a',label:'Khách đóng thêm',children:cash(selected.additionalAmount)},{key:'n',label:'Diễn giải',children:selected.settlementNotes||'—'},{key:'ref',label:'Chứng từ',children:selected.transferReference||'—'},{key:'s',label:'Xác nhận',children:(selected.landlordConfirmedAt?'Chủ nhà đã xác nhận':'Chủ nhà chưa xác nhận')+' · '+(selected.tenantConfirmedAt?'Khách đã xác nhận':'Khách chưa xác nhận')}]} />
   <Space wrap>{landlord&&['Approved','InspectionScheduled'].includes(selected.status)&&<Button onClick={()=>begin('schedule')}>Hẹn lịch bàn giao</Button>}{landlord&&(selected.status==='InspectionScheduled'||selected.status==='AwaitingSettlement'&&!selected.landlordConfirmedAt)&&<Button type="primary" onClick={()=>begin('settlement')}>Lập biên bản / quyết toán</Button>}{landlord&&selected.status==='AwaitingSettlement'&&!selected.landlordConfirmedAt&&<Button onClick={()=>begin('confirm')}>Xác nhận đã quyết toán</Button>}{landlord&&selected.status==='AwaitingSettlement'&&selected.landlordConfirmedAt&&<Button onClick={()=>begin('reopen')}>Mở lại quyết toán</Button>}{!landlord&&selected.status==='AwaitingSettlement'&&selected.landlordConfirmedAt&&<Button type="primary" onClick={()=>begin('confirm')}>Xác nhận bàn giao và số tiền thực tế</Button>}</Space>
  </Space>}</Modal>
  <Modal title={step==='schedule'?'Hẹn lịch bàn giao':step==='settlement'?'Biên bản và quyết toán':step==='reopen'?'Mở lại quyết toán':'Xác nhận thực tế'} open={!!step} onCancel={()=>setStep(undefined)} onOk={()=>void runStep()} confirmLoading={busy} okText="Xác nhận"><Form form={stepForm} layout="vertical" preserve={false}>
  {step==='schedule'&&<Form.Item name="date" label="Ngày giờ bàn giao" rules={[{required:true}]}><DatePicker showTime format="DD/MM/YYYY HH:mm" style={{width:'100%'}}/></Form.Item>}
  {step==='settlement'&&<><Form.Item name="inspectionNotes" label="Hiện trạng, nội thất, hư hỏng, chìa khóa" rules={[{required:true,max:4000}]}><Input.TextArea rows={4}/></Form.Item><Form.Item name="electricReading" label="Chỉ số điện"><InputNumber min={0}/></Form.Item><Form.Item name="waterReading" label="Chỉ số nước"><InputNumber min={0}/></Form.Item><Form.Item name="extraCharges" label="Phí phát sinh chưa lập hóa đơn (đ)"><InputNumber min={0} max={999999999}/></Form.Item><Form.Item name="settlementNotes" label="Diễn giải chi phí"><Input.TextArea maxLength={4000}/></Form.Item></>}
  {step==='confirm'&&<><Alert type="warning" message="Chỉ xác nhận sau khi đã bàn giao chìa khóa và thực tế nhận/chuyển tiền theo quyết toán." />{landlord&&<Form.Item name="reference" label="Chứng từ hoặc ghi chú" rules={[{required:true,max:1000}]}><Input.TextArea/></Form.Item>}</>}
  {step==='reopen'&&<p>Xóa xác nhận hiện tại để sửa quyết toán?</p>}
  </Form></Modal>
 </Page>;
}
