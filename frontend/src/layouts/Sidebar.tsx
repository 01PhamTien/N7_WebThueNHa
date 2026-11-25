import { Button, Drawer, Menu } from 'antd';
import { MenuOutlined } from '@ant-design/icons';
import { useState } from 'react';
import type { MenuProps } from 'antd';

export function Sidebar({
  items,
  selectedKey
}: {
  items: MenuProps['items'];
  selectedKey?: string;
}) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button
        className="mobile-menu-trigger"
        type="text"
        icon={<MenuOutlined />}
        aria-label="Mở điều hướng"
        onClick={() => setOpen(true)}
      />
      <Drawer
        title={<span className="drawer-brand">NhàỞ <small>Điều hướng</small></span>}
        placement="left"
        size="default"
        styles={{ wrapper: { width: 'min(340px, 90vw)' } }}
        open={open}
        onClose={() => setOpen(false)}
        className="mobile-navigation-drawer"
        destroyOnHidden
      >
        <Menu
          mode="inline"
          items={items}
          selectedKeys={selectedKey ? [selectedKey] : []}
          onClick={() => setOpen(false)}
        />
      </Drawer>
    </>
  );
}
