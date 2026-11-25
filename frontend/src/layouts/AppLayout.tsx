import { Layout } from 'antd';
import type { ReactNode } from 'react';

export function AppLayout({ header, children }: { header: ReactNode; children: ReactNode }) {
  return (
    <Layout className="layout">
      {header}
      <Layout.Content className="layout-content">{children}</Layout.Content>
      <Layout.Footer className="footer">
        <div className="footer-inner">
          <LinkPlaceholder />
          <span>© {new Date().getFullYear()} NhàỞ</span>
          <span>Tìm kiếm và thuê nhà thuận tiện hơn.</span>
        </div>
      </Layout.Footer>
    </Layout>
  );
}

function LinkPlaceholder() {
  return <span className="footer-brand">NhàỞ · Nền tảng kết nối thuê nhà</span>;
}
