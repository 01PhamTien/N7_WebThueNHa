# Checklist kiem thu tich hop thong bao nghiep vu

> Chỉ đánh dấu Passed sau khi chạy thật trên SQL Server test và đối chiếu bảng Notifications.

## Chuẩn bị
- Chạy migration mới nhất và API .NET 10 trên SQL Server test.
- Tạo Tenant A, Tenant B, Landlord A, Landlord B, Admin.
- Tạo Property đã Approved/Published, Booking, Contract Active/AwaitingSignature, Invoice và Conversation.
- Dùng JWT riêng cho từng vai trò; không ghi token thật vào Git.

## Ma trận kiểm thử

| Mã | Thao tác | Kết quả mong đợi | Kết quả thực tế |
|---|---|---|---|
| NI-01 | Tenant A tạo Booking | Chỉ Landlord sở hữu Property nhận Booking; ReferenceId=BookingId | Chưa chạy |
| NI-02 | Landlord Accept Booking | Tenant tạo Booking nhận Booking notification | Chưa chạy |
| NI-03 | Landlord Reject Booking | Tenant tạo Booking nhận Booking notification | Chưa chạy |
| NI-04 | Tenant Cancel Booking | Landlord sở hữu Property nhận Booking notification | Chưa chạy |
| NI-05 | Gọi Accept/Reject/Cancel sai quyền hoặc sai trạng thái | 403/409; không tạo notification | Chưa chạy |
| NI-06 | Tenant tạo hợp đồng từ Booking Accepted | Landlord nhận Contract notification; ReferenceId=ContractId | Chưa chạy |
| NI-07 | Landlord tạo hợp đồng từ Booking Accepted | Tenant nhận Contract notification; ReferenceId=ContractId | Chưa chạy |
| NI-08 | Landlord submit hợp đồng Draft | Tenant nhận Contract notification | Chưa chạy |
| NI-09 | Landlord ký hợp đồng | Tenant nhận thông báo bên kia đã ký | Chưa chạy |
| NI-10 | Tenant ký đủ chữ ký thứ hai | Landlord nhận thông báo hợp đồng đã Active | Chưa chạy |
| NI-11 | Cùng một bên gửi lại request ký | Không tạo notification mới | Chưa chạy |
| NI-12 | Tenant gửi tin nhắn văn bản | Landlord nhận Message notification; Sender không tự nhận | Chưa chạy |
| NI-13 | Landlord gửi tin nhắn văn bản | Tenant nhận Message notification; Sender không tự nhận | Chưa chạy |
| NI-14 | Gửi ảnh/PDF/Word trong chat | Participant còn lại nhận Message notification, ReferenceId=MessageId | Chưa chạy |
| NI-15 | SignalR client reconnect/retry event | Không phát sinh notification DB thứ hai | Chưa chạy |
| NI-16 | Landlord phát hành Invoice | Tenant nhận Invoice notification; ReferenceId=InvoiceId | Chưa chạy |
| NI-17 | Tenant gửi yêu cầu Payment Pending | Landlord nhận Payment notification; ReferenceId=PaymentId | Chưa chạy |
| NI-18 | Landlord/Admin xác nhận Payment | Tenant nhận Payment notification | Chưa chạy |
| NI-19 | Landlord/Admin từ chối Payment | Tenant nhận Payment notification kèm lý do | Chưa chạy |
| NI-20 | Tạo Invoice/Payment sai quyền hoặc trạng thái | 400/403/404/409; không tạo notification | Chưa chạy |
| NI-21 | Notification insert lỗi trong transaction nghiệp vụ | Rollback cả nghiệp vụ và notification | Chưa chạy |
| NI-22 | Kiểm tra type và ReferenceId | Type đúng enum; ReferenceId đúng entity liên quan | Chưa chạy |
| NI-23 | GET notifications/unread-count sau mỗi luồng | Người nhận thấy notification chưa đọc và unreadCount tăng đúng | Chưa chạy |

## Quy tắc xác nhận
- Mỗi thao tác nghiệp vụ thành công tạo đúng một notification cho đúng người nhận.
- Notification không được gửi cho Sender/actor nếu yêu cầu là thông báo bên còn lại.
- Notification được lưu trong cùng transaction với Booking/Contract/Message/Invoice/Payment.
- Không đánh dấu SQL Server test là Passed nếu chưa chạy thực tế.
- Ghi commit SHA, request, status code, UserId nhận và NotificationId.

