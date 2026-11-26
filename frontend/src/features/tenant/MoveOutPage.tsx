import {useEffect,useState} from 'react';
import {Alert,Button,Card,DatePicker ,Empty,Form,Input,Modal,Select,Space,Table,Tag,Typography,message} from 'antd';
import dayjs from 'dayjs';
import {Page} from '../../components/Shared';
import {tenantApi} from '../../services/tenant';
import {api} from '../../services/api';
import {errorText} from '../../services/api';
import type {ColumnsType} from 'antd/es/table';
type MoveOut={id:string;contractId:string;contractCode:string;propertyTitle:string;requestedMoveOutDate:string;reason:string;tenantNotes?:string;landlordNotes?:string;status:string;createdAt:string;tenantName:string;landlordName:string};
const labels:Record<string,string>={Pending:'Chờ chủ nhà phản hồi',Approved:'Đã chấp nhận',Rejected:'Đã từ chối',Cancelled:'Đã hủy',InspectionScheduled:'Đã hẹn bàn giao',AwaitingSettlement:'Chờ quyết toán',Completed:'Đã hoàn tất'};
const colors:Record<string,string>={Pending:'gold',Approved:'blue',Rejected:'red',Cancelled:'default',Completed:'green',InspectionScheduled:'processing',AwaitingSettlement:'orange'};
export function MoveOutPage({landlord=false}:{landlord?:boolean}){
 const [rows,setRows]=useState<MoveOut[]>([]);const [contracts,setContracts]=useState<any[]>([]);const [loading,setLoading]=useState(false);const [open,setOpen]=useState(false);const [form]=Form.useForm();const [busy,setBusy]=useState(false);
 const load=async()=>{setLoading(true);try{const r=await api.get<MoveOut[]>('/api/move-outs');setRows(r.data);if(!landlord){const c=await tenantApi.contracts(1,50);setContracts(c.data.items.filter((x:any)=>x.status==='Active'));}}catch(e){message.error(errorText(e));}finally{setLoading(false);}};
 useEffect(()=>{void load();},[]);
 const submit=async()=>{try{const v=await form.validateFields();setBusy(true);await tenantApi.createMoveOut({contractId:v.contractId,requestedMoveOutDate:v.date.format('YYYY-MM-DD'),reason:v.reason,tenantNotes:v.notes});message.success('Đã gửi yêu cầu trả nhà');setOpen(false);form.resetFields();await load();}catch(e){if(e instanceof Error&&e.message)message.error(errorText(e));}finally{setBusy(false);}};
 const respond=async(id:string,approve:boolean)=>{try{await api.post('/api/move-outs/'+id+'/landlord-response',{approve});message.success(approve?'Đã chấp nhận yêu cầu':'Đã từ chối yêu cầu');await load();}catch(e){message.error(errorText(e));}};
 const cancel=async(id:string)=>{try{await tenantApi.cancelMoveOut(id);message.success('Đã hủy yêu cầu');await load();}catch(e){message.error(errorText(e));}};
 const columns:ColumnsType<MoveOut>=[
  {title:'Căn nhà',dataIndex:'propertyTitle'},
  {title:landlord?'Người thuê':'Mã hợp đồng',dataIndex:landlord?'tenantName':'contractCode'},
  {title:'Ngày dự kiến trả',dataIndex:'requestedMoveOutDate',render:v=>dayjs(v).format('DD/MM/YYYY')},
  {title:'Trạng thái',dataIndex:'status',render:v=><Tag color={colors[v]||'default'}>{labels[v]||v}</Tag>},
  {title:'Lý do',dataIndex:'reason',ellipsis:true},
  {title:'Thao tác',render:(_,r)=><Space>{landlord&&r.status==='Pending'&&<><Button size="small" type="primary" onClick={()=>void respond(r.id,true)}>Chấp nhận</Button><Button size="small" danger onClick={()=>void respond(r.id,false)}>Từ chối</Button></>}{!landlord&&['Pending','Approved'].includes(r.status)&&<Button size="small" danger onClick={()=>void cancel(r.id)}>Hủy yêu cầu</Button>}</Space>}
 ];
 return <Page title={landlord?'Quản lý trả nhà':'Yêu cầu trả nhà'} description={landlord?'Tiếp nhận yêu cầu và thống nhất lịch trả nhà với người thuê.':'Gửi yêu cầu trả nhà và theo dõi phản hồi từ chủ nhà.'}>
  {!landlord&&<Alert type="info" showIcon style={{marginBottom:16}} message="Gửi yêu cầu không tự động kết thúc hợp đồng." description="Hợp đồng chỉ được xem xét kết thúc sau khi hai bên hoàn tất bàn giao và đối soát tiền thuê, phí phát sinh, công nợ và tiền cọc."/>}
  <Card title={landlord?'Yêu cầu trả nhà đến':'Các yêu cầu trả nhà của tôi'} extra={!landlord&&<Button type="primary" onClick={()=>setOpen(true)}>Tạo yêu cầu trả nhà</Button>}>
   <Table rowKey="id" loading={loading} columns={columns} dataSource={rows} pagination={{pageSize:10}} locale={{emptyText:<Empty description="Chưa có yêu cầu trả nhà"/>}}/>
  </Card>
  <Modal title="Gửi yêu cầu trả nhà" open={open} onCancel={()=>setOpen(false)} onOk={()=>void submit()} confirmLoading={busy} okText="Gửi yêu cầu">
   <Form form={form} layout="vertical">
    <Form.Item name="contractId" label="Hợp đồng đang hiệu lực" rules={[{required:true,message:'Chọn hợp đồng'}]}><SelectContract contracts={contracts}/></Form.Item>
    <Form.Item name="date" label="Ngày dự kiến trả nhà" rules={[{required:true,message:'Chọn ngày trả nhà'}]}><DatePicker style={{width:'100%'}} minDate={dayjs()} format="DD/MM/YYYY"/></Form.Item>
    <Form.Item name="reason" label="Lý do trả nhà" rules={[{required:true,min:3,max:1000}]}><Input.TextArea rows={3} maxLength={1000}/></Form.Item>
    <Form.Item name="notes" label="Ghi chú thêm"><Input.TextArea rows={2} maxLength={2000}/></Form.Item>
   </Form>
  </Modal>
 </Page>;
}
function SelectContract({contracts}:{contracts:any[]}){return <Select options={contracts.map(x=>({value:x.id,label:x.contractCode+' · '+x.propertyTitle}))} placeholder="Chọn hợp đồng"/>}
