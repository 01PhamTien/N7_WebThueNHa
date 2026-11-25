import { Button, Drawer, Menu } from 'antd';
import { MenuOutlined } from '@ant-design/icons';
import { useState } from 'react';
import type { MenuProps } from 'antd';
export function Sidebar({items}:{items:MenuProps['items']}) {
  const [open,setOpen]=useState(false);
  return <><Button className="mobile-menu-trigger" type="text" icon={<MenuOutlined/>} aria-label="Mở điều hướng" onClick={()=>setOpen(true)}/><Drawer title="Điều hướng" placement="left" open={open} onClose={()=>setOpen(false)}><Menu mode="inline" items={items} onClick={()=>setOpen(false)}/></Drawer></>;
}