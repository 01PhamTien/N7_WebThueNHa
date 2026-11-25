import { Layout } from 'antd';
import { HomeOutlined } from '@ant-design/icons';
import type { ReactNode } from 'react';

export function AppLayout({ header, children }: { header: ReactNode; children: ReactNode }) {
  return (
    <Layout className="layout">
      {header}
      <Layout.Content className="layout-content">{children}</Layout.Content>
      <Layout.Footer className="footer">
        <div className="footer-inner">
          <div className="footer-brand-block">
            <span className="footer-brand-mark"><HomeOutlined /></span>
            <div>
              <strong>NhàỞ</strong>
              <p>Nền tảng kết nối người thuê và chủ nhà.</p>
            </div>
          </div>
          <div className="footer-note">
            <span>© {new Date().getFullYear()} NhàỞ</span>
            <span>Tìm đúng nơi ở, an tâm bắt đầu hành trình mới.</span>
          </div>
        </div>
      </Layout.Footer>
    </Layout>
  );
}
