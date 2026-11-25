import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, App as AntApp, Button, Empty, Input, List, Select, Space, Spin, Tag, Upload } from 'antd';
import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';
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
  const conn = useRef<HubConnection | null>(null);
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
    setLoadingMessages(true);
    setError('');
    try {
      const response = await tenantApi.messages(conversation.conversationId, 1, SIZE);
      setMessages(response.data.items || []);
      setMessagePage(1);
      setHasOlder(response.data.hasNext);
      await tenantApi.markChatRead(conversation.conversationId);
    } catch (error) {
      setError(errorText(error));
    } finally {
      setLoadingMessages(false);
    }
  }, []);

  const openConversation = (conversation: ConversationItem) => {
    setActive(conversation);
    void loadMessages(conversation);
  };

  useEffect(() => {
    const id = (location.state as { conversationId?: string } | null)?.conversationId;
    if (!id) return;
    const found = conversations.find(item => item.conversationId === id);
    if (found) openConversation(found);
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
    conn.current = connectionInstance;

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
      conn.current = null;
    };
  }, [active?.conversationId, user?.userId]);

  useEffect(() => {
    if (scroll.current) scroll.current.scrollTop = scroll.current.scrollHeight;
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
      loadConversations();
    } catch (error) {
      setText(content);
      message.error(errorText(error));
    }
  };

  const loadOlderMessages = async () => {
    if (!active || !hasOlder) return;
    const nextPage = messagePage + 1;
    setLoadingMessages(true);
    try {
      const response = await tenantApi.messages(active.conversationId, nextPage, SIZE);
      setMessages(previous => [
        ...response.data.items,
        ...previous.filter(item => !response.data.items.some(next => next.messageId === item.messageId))
      ]);
      setMessagePage(nextPage);
      setHasOlder(response.data.hasNext);
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setLoadingMessages(false);
    }
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

  return (
    <main className="page">
      <h1>Tin nhắn</h1>
      {error && <Alert type="error" showIcon message={error} closable onClose={() => setError('')} />}
      <div className="chat">
        <aside>
          <div className="chat-inbox-toolbar">
            <Select
              size="small"
              value={includeArchived ? 'archived' : 'inbox'}
              onChange={value => {
                setIncludeArchived(value === 'archived');
                setPage(1);
                setActive(null);
                setMessages([]);
              }}
              options={[
                { value: 'inbox', label: 'Hộp thư đến' },
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
              <div className="messages" ref={scroll}>
                {hasOlder && <Button type="link" loading={loadingMessages} onClick={() => void loadOlderMessages()}>Tải tin nhắn cũ hơn</Button>}
                {loadingMessages && <Spin />}
                {messages.map(item => (
                  <article key={item.messageId} className={item.isMine ? 'bubble mine' : 'bubble'}>
                    <small>{item.senderName} · {new Date(item.sentAt).toLocaleString('vi-VN')}</small>
                    {item.content && <p>{item.content}</p>}
                    {item.attachments.map(attachment => (
                      <p key={attachment.attachmentId}>
                        <a href={(import.meta.env.VITE_API_BASE_URL || '') + attachment.downloadUrl} target="_blank" rel="noreferrer">
                          {attachment.fileName}
                        </a>
                      </p>
                    ))}
                  </article>
                ))}
              </div>
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
                    tenantApi.uploadAttachment(active.conversationId, file)
                      .then(() => tenantApi.messages(active.conversationId, 1, SIZE))
                      .then(response => setMessages(response.data.items))
                      .then(() => message.success('Đã gửi tệp'))
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
    </main>
  );
}
