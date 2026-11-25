import { Alert, Empty, Modal, Spin, Button } from 'antd';
import type { ReactNode } from 'react';

export function Page({title,children,description}:{title:string;children:ReactNode;description?:string}) {
  return <main className="page"><h1>{title}</h1>{description&&<p className="muted">{description}</p>}{children}</main>;
}
export function Loading(){return <div className="center"><Spin size="large"/></div>}
export function EmptyState({description='Chưa có dữ liệu'}:{description?:string}){return <Empty description={description}/>}
export function ErrorState({message}:{message:string}){return <Alert type="error" showIcon message={message}/>}
export function ConfirmModal({title,content,triggerLabel,onConfirm}:{title:string;content:string;triggerLabel:string;onConfirm:()=>void|Promise<void>}) {
  return <Button danger onClick={()=>Modal.confirm({title,content,okText:'Xác nhận',cancelText:'Hủy',onOk:onConfirm})}>{triggerLabel}</Button>;
}
