import { useEffect, useState } from 'react';
import type { DependencyList } from 'react';
import { Alert, Button, Space, Tag, Typography } from 'antd';
import { errorText } from '../../services/api';

export const money = (v?: number) => (v ?? 0).toLocaleString('vi-VN') + ' đ';
export const dateText = (v?: string) => v ? new Date(v).toLocaleDateString('vi-VN') : '—';
export const bookingStatusLabels: Record<number, string> = { 0: 'Chờ xử lý', 1: 'Đã chấp nhận', 2: 'Đã từ chối', 3: 'Đã hủy', 4: 'Hết hạn' };
export const moderationStatusLabels: Record<number, string> = { 0: 'Bản nháp', 1: 'Chờ duyệt', 2: 'Đã duyệt', 3: 'Đã từ chối' };
export const listingStatusLabels: Record<string, string> = { Draft: 'Bản nháp', Published: 'Đang đăng', Hidden: 'Đã ẩn', Closed: 'Đã đóng', '0': 'Bản nháp', '1': 'Đang đăng', '2': 'Đã ẩn', '3': 'Đã đóng' };
export const contractStatusLabels: Record<string, string> = { Draft: 'Bản nháp', AwaitingSignature: 'Chờ ký', Active: 'Đang hiệu lực', Completed: 'Hoàn thành', Terminated: 'Đã chấm dứt', Cancelled: 'Đã hủy' };
export const invoiceStatusLabels: Record<string, string> = { Draft: 'Bản nháp', Issued: 'Đã phát hành', PartiallyPaid: 'Đã thanh toán một phần', Paid: 'Đã thanh toán', Overdue: 'Quá hạn', Cancelled: 'Đã hủy' };
export const invoiceTypeLabels: Record<string, string> = { Deposit: 'Tiền cọc', MonthlyRent: 'Tiền thuê hàng tháng', Other: 'Khác' };
export const paymentStatusLabels: Record<string, string> = { Pending: 'Chờ xác nhận', Succeeded: 'Thành công', Failed: 'Thất bại', Cancelled: 'Đã hủy', Refunded: 'Đã hoàn tiền' };
export const moderationActionLabels: Record<number, string> = { 0: 'Đã duyệt', 1: 'Từ chối', 2: 'Đã ẩn', 3: 'Đã khôi phục' };
const statusColor = (label: string) => ({ 'Chờ xử lý': 'gold', 'Chờ duyệt': 'gold', 'Chờ xác nhận': 'gold', 'Đã duyệt': 'green', 'Đang đăng': 'green', 'Đã chấp nhận': 'green', 'Đang hiệu lực': 'green', 'Đã thanh toán': 'green', 'Thành công': 'green', 'Đã từ chối': 'red', 'Từ chối': 'red', 'Thất bại': 'red', 'Đã hủy': 'red', 'Quá hạn': 'red', 'Đã ẩn': 'orange', 'Hết hạn': 'orange', 'Chờ ký': 'blue' } as Record<string, string>)[label] || 'default';
export function Status({ value, label }: { value: string | number; label?: string }) {
  const display = label ?? listingStatusLabels[String(value)] ?? contractStatusLabels[String(value)] ?? invoiceStatusLabels[String(value)] ?? paymentStatusLabels[String(value)] ?? String(value);
  return <Tag color={statusColor(display)}>{display}</Tag>;
}
export function useRequest<T>(load: () => Promise<{ data: T }>, deps: DependencyList = []) {
  const [data,setData] = useState<T | null>(null); const [busy,setBusy] = useState(true); const [error,setError] = useState('');
  const refresh = async () => { setBusy(true); setError(''); try { setData((await load()).data); } catch(e) { setError(errorText(e)); } finally { setBusy(false); } };
  useEffect(() => { void refresh(); }, deps);
  return { data, busy, error, refresh, setData };
}
export function ErrorLine({error}:{error:string}) { return error ? <Alert type="error" showIcon message={error} style={{marginBottom:16}}/> : null; }
export function Paging({page,total,onChange}:{page:number;total:number;onChange:(p:number)=>void}) {
  return <div style={{display:'flex',justifyContent:'flex-end',paddingTop:16}}><Space><Button disabled={page<=1} onClick={()=>onChange(page-1)}>Trước</Button><Typography.Text>Trang {page} · {total} mục</Typography.Text><Button disabled={page*10>=total} onClick={()=>onChange(page+1)}>Sau</Button></Space></div>;
}

