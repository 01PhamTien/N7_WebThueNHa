import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, App as AntApp, Button, Empty, Input, List, Modal, Popconfirm, Segmented, Space, Spin, Tag, Upload } from 'antd';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useLocation } from 'react-router-dom';
import { tenantApi } from '../../services/tenant';
import { errorText, TOKEN_KEY } from '../../services/api';
import { useAuth } from '../../app/AuthContext';
import type { ConversationItem, ChatMessage } from '../../types/api';

const SIZE = 20;

export function ChatPage() {
  const { message } = AntApp.useApp();
  const { user } = useAuth();
  const location = useLocation();
  const [conversations, setConversations] = useState<ConversationItem[]>([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(0);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [active, setActive] = useState<ConversationItem | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [messagePage, setMessagePage] = useState(1);
  const [hasOlder, setHasOlder] = useState(false);
  const [text, setText] = useState('');
  const [busy, setBusy] = useState(true);
  const [loadingMessages, setLoadingMessages] = useState(false);
  const [error, setError] = useState('');
  const [connection, setConnection] = useState('Đang kết nối');
  const [archiving, setArchiving] = useState(false);
  const [editingMessage, setEditingMessage] = useState<ChatMessage | null>(null);
  const [editContent, setEditContent] = useState('');
  const [savingMessage, setSavingMessage] = useState(false);
  const [deletingMessageId, setDeletingMessageId] = useState<string | null>(null);
  const messageRequestId = useRef(0);
  const lastAutoOpenedConversation = useRef<string | null>(null);
  const shouldAutoScroll = useRef(true);
  const scroll = useRef<HTMLDivElement>(null);

  const loadConversations = useCallback(() => {
    setBusy(true);
    tenantApi.conversations(page, SIZE, includeArchived)
      .then(response => {
        setConversations(response.data.items || []);
        setTotalPages(response.data.totalPages || 0);
      })
      .catch(error => setError(errorText(error)))
      .finally(() => setBusy(false));
  }, [page, includeArchived]);

  useEffect(() => { loadConversations(); }, [loadConversations]);

  const loadMessages = useCallback(async (conversation: ConversationItem) => {
    const requestId = ++messageRequestId.current;
    setLoadingMessages(true);
    setError('');
    try {
      const response = await tenantApi.messages(conversation.conversationId, 1, SIZE);
      if (requestId !== messageRequestId.current) return;
      setMessages(response.data.items || []);
      setMessagePage(1);
      setHasOlder(response.data.hasNext);
      await tenantApi.markChatRead(conversation.conversationId);
      if (requestId === messageRequestId.current) loadConversations();
    } catch (error) {
      if (requestId === messageRequestId.current) setError(errorText(error));
    } finally {
      if (requestId === messageRequestId.current) setLoadingMessages(false);
    }
  }, [loadConversations]);

  const openConversation = (conversation: ConversationItem) => {
    shouldAutoScroll.current = true;
    setActive(conversation);
    setConnection('Đang kết nối');
    void loadMessages(conversation);
  };

  useEffect(() => {
    const id = (location.state as { conversationId?: string } | null)?.conversationId;
    if (!id || lastAutoOpenedConversation.current === id) return;
    const found = conversations.find(item => item.conversationId === id);
    if (found) {
      lastAutoOpenedConversation.current = id;
      openConversation(found);
    }
  }, [conversations, location.state]);

  useEffect(() => {
    if (!active) return;
    let mounted = true;
    const connectionInstance = new HubConnectionBuilder()
      .withUrl((import.meta.env.VITE_API_BASE_URL || window.location.origin) + '/hubs/chat', {
        accessTokenFactory: () => sessionStorage.getItem(TOKEN_KEY) || ''
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connectionInstance.on('NhanTinNhan', (incoming: {
      messageId: string;
      conversationId: string;
      senderId: string;
      senderName: string;
      content: string;
      messageType: string;
      sentAt: string;
    }) => {
      if (incoming.conversationId !== active.conversationId) return;
      setMessages(previous => previous.some(item => item.messageId === incoming.messageId)
        ? previous
        : [...previous, { ...incoming, isMine: incoming.senderId === user?.userId, attachments: [] }]);
      if (incoming.senderId !== user?.userId) void tenantApi.markChatRead(active.conversationId);
    });

    connectionInstance.on('SuaTinNhan', (updated: { messageId: string; content: string; editedAt: string }) => {
      if (!mounted) return;
      setMessages(previous => previous.map(item => item.messageId === updated.messageId ? { ...item, content: updated.content, editedAt: updated.editedAt } : item));
    });
    connectionInstance.on('XoaTinNhan', (deleted: { messageId: string }) => {
      if (!mounted) return;
      setMessages(previous => previous.filter(item => item.messageId !== deleted.messageId));
    });

    connectionInstance.onreconnecting(() => { if (mounted) setConnection('Đang kết nối lại'); });
    connectionInstance.onreconnected(() => { if (mounted) setConnection('Đã kết nối'); });
    connectionInstance.onclose(() => { if (mounted) setConnection('Mất kết nối'); });

    void connectionInstance.start()
      .then(() => connectionInstance.invoke('JoinConversation', active.conversationId))
      .then(() => { if (mounted) setConnection('Đã kết nối'); })
      .catch(() => { if (mounted) setConnection('Mất kết nối'); });

    return () => {
      mounted = false;
      void connectionInstance.invoke('LeaveConversation', active.conversationId).catch(() => undefined);
      void connectionInstance.stop();
    };
  }, [active?.conversationId, user?.userId]);

  useEffect(() => {
    if (scroll.current && shouldAutoScroll.current) {
      scroll.current.scrollTop = scroll.current.scrollHeight;
    }
  }, [messages.length, active?.conversationId]);

  const send = async () => {
    const content = text.trim();
    if (!active || !content) return;
    setText('');
    try {
      const response = await tenantApi.sendMessage(active.conversationId, content);
      const sent = response.data;
      setMessages(previous => previous.some(item => item.messageId === sent.messageId)
        ? previous
        : [...previous, { ...sent, isMine: true, attachments: [] }]);
      if (active.isArchived) {
        setActive(previous => previous ? { ...previous, isArchived: false } : previous);
        setIncludeArchived(false);
        setPage(1);
      }
      loadConversations();
    } catch (error) {
      setText(content);
      message.error(errorText(error));
    }
  };

  const loadOlderMessages = async () => {
    if (!active || !hasOlder) return;
    const conversationId = active.conversationId;
    const requestId = messageRequestId.current;
    const nextPage = messagePage + 1;
    const previousHeight = scroll.current?.scrollHeight || 0;
    shouldAutoScroll.current = false;
    setLoadingMessages(true);
    try {
      const response = await tenantApi.messages(conversationId, nextPage, SIZE);
      if (requestId !== messageRequestId.current) return;
      setMessages(previous => [
        ...response.data.items,
        ...previous.filter(item => !response.data.items.some(next => next.messageId === item.messageId))
      ]);
      setMessagePage(nextPage);
      setHasOlder(response.data.hasNext);
      requestAnimationFrame(() => {
        if (scroll.current && requestId === messageRequestId.current) {
          scroll.current.scrollTop += scroll.current.scrollHeight - previousHeight;
        }
      });
    } catch (error) {
      if (requestId === messageRequestId.current) message.error(errorText(error));
    } finally {
      if (requestId === messageRequestId.current) setLoadingMessages(false);
    }
  };

  const saveEditedMessage = async () => {
    if (!active || !editingMessage) return;
    const content = editContent.trim();
    if (!content) { message.warning('Tin nhắn không được để trống.'); return; }
    setSavingMessage(true);
    try {
      const response = await tenantApi.editMessage(active.conversationId, editingMessage.messageId, content);
      setMessages(previous => previous.map(item => item.messageId === editingMessage.messageId ? { ...item, content: response.data.content, editedAt: response.data.editedAt } : item));
      setEditingMessage(null);
      setEditContent('');
    } catch (error) { message.error(errorText(error)); }
    finally { setSavingMessage(false); }
  };

  const deleteMessage = async (item: ChatMessage) => {
    if (!active) return;
    setDeletingMessageId(item.messageId);
    try {
      await tenantApi.deleteMessage(active.conversationId, item.messageId);
      setMessages(previous => previous.filter(messageItem => messageItem.messageId !== item.messageId));
      message.success('Đã xóa tin nhắn.');
      loadConversations();
    } catch (error) { message.error(errorText(error)); }
    finally { setDeletingMessageId(null); }
  };

  const archiveConversation = async () => {
    if (!active) return;
    setArchiving(true);
    try {
      await tenantApi.archiveConversation(active.conversationId, !active.isArchived);
      message.success(active.isArchived ? 'Đã khôi phục hội thoại' : 'Đã lưu trữ hội thoại');
      setActive(null);
      setMessages([]);
      loadConversations();
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setArchiving(false);
    }
  };

  const downloadAttachment = async (attachmentId: string, fileName: string) => {
    try {
      const response = await tenantApi.downloadAttachment(attachmentId);
      const url = URL.createObjectURL(response.data);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (error) {
      message.error(errorText(error));
    }
  };

  return (
    <main className="page">
      <h1>Tin nhắn</h1>
      {error && <Alert type="error" showIcon message={error} closable onClose={() => setError('')} />}
      <div className="chat">
        <aside>
          <div className="chat-inbox-toolbar">
            <Segmented
              size="middle"
              block
              value={includeArchived ? 'archived' : 'active'}
              onChange={value => {
                setIncludeArchived(value === 'archived');
                setPage(1);
                setActive(null);
                setMessages([]);
                setText('');
                messageRequestId.current += 1;
              }}
              options={[
                { value: 'active', label: 'Đang hoạt động' },
                { value: 'archived', label: 'Đã lưu trữ' }
              ]}
              aria-label="Chọn danh sách hội thoại"
            />
          </div>
          {busy ? <Spin /> : (
            <List
              dataSource={conversations}
              locale={{ emptyText: <Empty description={includeArchived ? 'Chưa có hội thoại đã lưu trữ' : 'Chưa có cuộc trò chuyện'} /> }}
              renderItem={conversation => (
                <List.Item
                  className={active?.conversationId === conversation.conversationId ? 'conversation-active' : ''}
                  onClick={() => openConversation(conversation)}
                >
                  <List.Item.Meta
                    title={conversation.propertyTitle}
                    description={conversation.lastMessageContent || 'Chưa có tin nhắn'}
                  />
                  {conversation.unreadCount > 0 && <Tag color="blue">{conversation.unreadCount}</Tag>}
                </List.Item>
              )}
            />
          )}
          {totalPages > 1 && (
            <div className="chat-paging">
              <Button disabled={page <= 1} onClick={() => setPage(value => value - 1)}>Trước</Button>
              <span>{page}/{totalPages}</span>
              <Button disabled={page >= totalPages} onClick={() => setPage(value => value + 1)}>Sau</Button>
            </div>
          )}
        </aside>

        <section className="chat-panel">
          {!active ? <Empty description="Chọn một cuộc trò chuyện để xem tin nhắn" /> : (
            <>
              <header>
                <strong>{active.propertyTitle}</strong>
                <Space>
                  <Tag color={connection === 'Đã kết nối' ? 'green' : 'orange'}>{connection}</Tag>
                  <Button size="small" loading={archiving} onClick={() => void archiveConversation()}>
                    {active.isArchived ? 'Đưa về hộp thư đến' : 'Lưu trữ'}
                  </Button>
                </Space>
              </header>
              <div
                className="messages"
                ref={scroll}
                onScroll={() => {
                  const node = scroll.current;
                  if (node) {
                    shouldAutoScroll.current =
                      node.scrollHeight - node.scrollTop - node.clientHeight < 80;
                  }
                }}
              >
                {hasOlder && <Button type="link" loading={loadingMessages} onClick={() => void loadOlderMessages()}>Tải tin nhắn cũ hơn</Button>}
                {loadingMessages && <Spin />}
                {messages.map(item => (
                  <article key={item.messageId} className={item.isMine ? 'bubble mine' : 'bubble'}>
                    <small>{item.senderName} · {new Date(item.sentAt).toLocaleString('vi-VN')}{item.editedAt ? ' · Đã chỉnh sửa' : ''}</small>
                    {item.content && <p>{item.content}</p>}
                    {item.isMine && item.messageType === 'Text' && (
                      <div className="chat-message-actions">
                        <Button type="link" size="small" onClick={() => { setEditingMessage(item); setEditContent(item.content || ''); }}>Sửa</Button>
                        <Popconfirm title="Xóa tin nhắn này?" description="Tin nhắn sẽ bị gỡ khỏi cuộc trò chuyện." okText="Xóa" cancelText="Hủy" okButtonProps={{ danger: true, loading: deletingMessageId === item.messageId }} onConfirm={() => void deleteMessage(item)}>
                          <Button type="link" danger size="small" loading={deletingMessageId === item.messageId}>Xóa</Button>
                        </Popconfirm>
                      </div>
                    )}
                    {item.attachments.map(attachment => (
                      <p key={attachment.attachmentId}>
                        <Button
                          type="link"
                          size="small"
                          onClick={() => void downloadAttachment(attachment.attachmentId, attachment.fileName)}
                        >
                          {attachment.fileName}
                        </Button>
                      </p>
                    ))}
                  </article>
                ))}
              </div>
              {active.isArchived && (
                <Alert
                  type="info"
                  showIcon
                  message="Hội thoại đã lưu trữ"
                  description="Khi gửi tin nhắn hoặc tệp mới, hội thoại sẽ tự chuyển về Hộp thư đến."
                  style={{ marginBottom: 8 }}
                />
              )}
              <Space.Compact className="composer">
                <Input
                  value={text}
                  onChange={event => setText(event.target.value)}
                  onPressEnter={() => void send()}
                  maxLength={4000}
                  placeholder="Nhập tin nhắn..."
                />
                <Button type="primary" onClick={() => void send()} disabled={!text.trim()}>Gửi</Button>
                <Upload
                  showUploadList={false}
                  accept=".jpg,.jpeg,.png,.webp,.pdf,.doc,.docx"
                  beforeUpload={file => {
                    const extension = file.name.split('.').pop()?.toLowerCase() || '';
                    const allowed = ['jpg', 'jpeg', 'png', 'webp', 'pdf', 'doc', 'docx'];
                    if (file.size > 10 * 1024 * 1024) {
                      message.error('Tệp đính kèm không được vượt quá 10 MB.');
                      return Upload.LIST_IGNORE;
                    }
                    if (!allowed.includes(extension)) {
                      message.error('Chỉ hỗ trợ JPG, JPEG, PNG, WEBP, PDF và DOC/DOCX.');
                      return Upload.LIST_IGNORE;
                    }
                    tenantApi.uploadAttachment(active.conversationId, file)
                      .then(() => tenantApi.messages(active.conversationId, 1, SIZE))
                      .then(response => {
                        setMessages(response.data.items || []);
                        setMessagePage(1);
                        setHasOlder(response.data.hasNext);
                        setActive(previous => previous ? { ...previous, isArchived: false } : previous);
                        setIncludeArchived(false);
                        setPage(1);
                      })
                      .then(() => {
                        loadConversations();
                        message.success('Đã gửi tệp');
                      })
                      .catch(error => message.error(errorText(error)));
                    return false;
                  }}
                >
                  <Button>Tệp</Button>
                </Upload>
              </Space.Compact>
            </>
          )}
        </section>
      </div>
      <Modal title="Chỉnh sửa tin nhắn" open={!!editingMessage} onCancel={() => { setEditingMessage(null); setEditContent(''); }} onOk={() => void saveEditedMessage()} okText="Lưu" cancelText="Hủy" confirmLoading={savingMessage} okButtonProps={{ disabled: !editContent.trim() }}>
        <Input.TextArea value={editContent} onChange={event => setEditContent(event.target.value)} autoSize={{ minRows: 3, maxRows: 6 }} maxLength={4000} showCount />
      </Modal>
    </main>
  );
}
