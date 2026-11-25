import { Alert, Empty, App as AntApp, Spin, Button } from 'antd';
import type { ReactNode } from 'react';

export function Page({
  title,
  children,
  description
}: {
  title: string;
  children: ReactNode;
  description?: string;
}) {
  return (
    <main className="page page-shell">
      <header className="page-heading">
        <div>
          <h1>{title}</h1>
          {description && <p className="muted">{description}</p>}
        </div>
      </header>
      <div className="page-content">{children}</div>
    </main>
  );
}

export function Loading() {
  return <div className="center"><Spin size="large" /></div>;
}

export function EmptyState({ description = 'Chưa có dữ liệu' }: { description?: string }) {
  return <Empty description={description} />;
}

export function ErrorState({ message }: { message: string }) {
  return <Alert type="error" showIcon message={message} />;
}

export function ConfirmModal({
  title,
  content,
  triggerLabel,
  onConfirm
}: {
  title: string;
  content: string;
  triggerLabel: string;
  onConfirm: () => void | Promise<void>;
}) {
  const { modal } = AntApp.useApp();

  return (
    <Button
      danger
      onClick={() => modal.confirm({
        title,
        content,
        okText: 'Xác nhận',
        cancelText: 'Hủy',
        onOk: onConfirm
      })}
    >
      {triggerLabel}
    </Button>
  );
}
