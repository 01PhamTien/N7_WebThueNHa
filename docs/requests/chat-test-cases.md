# Checklist kiem thu Chat theo tung can nha

> Trang thai thuc thi: Chua chay tren SQL Server/SignalR local. Khong danh dau Dat neu chua gui request that.

## Chuan bi
- Tao 1 Landlord co can nha Approved + Published.
- Tao 2 Tenant khac nhau.
- Dang nhap 3 tai khoan, dien JWT va PropertyId/ConversationId vao `Requests/chat.http`.
- Chay API tren HTTPS va mo hai client SignalR rieng.

## Ma tran kiem thu

| Ma | Thao tac | Ket qua mong doi | Thuc te |
|---|---|---|---|
| C01 | Tenant tao conversation cho property Published + Approved | 201; tao dung mot conversation | Chua chay |
| C02 | Tenant goi lai endpoint cho cung property | 200; tra lai conversationId cu, khong tao trung | Chua chay |
| C03 | Tenant tao conversation cho property chua duoc duyet/an | 409 | Chua chay |
| C04 | Landlord tu tao conversation voi nha cua minh | 400 | Chua chay |
| C05 | Khong co JWT goi API chat | 401 | Chua chay |
| C06 | Tenant A doc conversation cua Tenant B | 404 | Chua chay |
| C07 | Tenant A gui tin vao conversation khong tham gia | 404 | Chua chay |
| C08 | Gui noi dung rong/chi co khoang trang | 400 | Chua chay |
| C09 | Danh sach conversation page/pageSize hop le | 200; chi participant cua user hien tai; item co unreadCount | Chua chay |
| C10 | includeArchived=false/true | Mac dinh an conversation archived; true hien ca hai | Chua chay |
| C11 | page=0, pageSize=0 hoac vuot max | 400 | Chua chay |
| C12 | Page vuot totalPages | 200; items rong va metadata dung | Chua chay |
| C13 | Lich su tin nhan page 1/page 2 | Khong trung tin; thu tu trong tung trang tang dan theo SentAt/Id | Chua chay |
| C14 | Tenant A sua/xoa tin cua chinh minh | Sua 200; xoa mem 204 | Chua chay |
| C15 | Tenant A sua/xoa tin cua nguoi khac | 404 | Chua chay |
| C16 | MarkAsRead | LastReadAt cap nhat; unreadCount giam khi tai lai danh sach | Chua chay |
| C17 | Archive/Unarchive | Chi thay doi trang thai cua participant hien tai | Chua chay |
| C18 | Upload JPG/PNG/PDF hop le, duoi 10 MB | 201; tep luu ngoai wwwroot | Chua chay |
| C19 | Upload tep >10 MB, extension/MIME/magic bytes khong khop | 400 | Chua chay |
| C20 | Tenant A tai tep cua conversation B | 404 | Chua chay |
| C21 | Xoa message co attachment | Tin an khoi lich su; download attachment tra 404 | Chua chay |
| C22 | Hai client participant JoinConversation | Ca hai nhan event NhanTinNhan/SuaTinNhan/XoaTinNhan tu group | Chua chay |
| C23 | User khong phai participant JoinConversation | HubException; khong vao group | Chua chay |
| C24 | Goi LeaveConversation | Chi connection hien tai roi group; khong anh huong participant khac | Chua chay |

## Ghi nhan
- Ghi commit SHA, .NET SDK, SQL Server va status code thuc te.
- Kiem tra file that duoc tao trong `PrivateUploads/chat`; khong commit tep upload.
- Khong dua JWT, mat khau hoac thong tin ca nhan that len GitHub.
