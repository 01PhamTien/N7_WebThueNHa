import { Layout } from 'antd';
import type { ReactNode } from 'react';
export function AppLayout({header,children}:{header:ReactNode;children:ReactNode}) {
  return <Layout className="layout">{header}<Layout.Content>{children}</Layout.Content><Layout.Footer className="footer">© {new Date().getFullYear()} NhàỞ · Tìm kiếm và thuê nhà</Layout.Footer></Layout>;
}