# Checklist kiem thu SePay Gateway va Bank Webhook

> Chỉ đánh dấu Passed sau khi chạy trên SQL Server test và SePay Sandbox. Không dùng secret Production trong file/request mẫu.

## Chuẩn bị
- Chạy migration mới nhất của dự án.
- Cấu hình các key `SEPAY_*` bằng User Secrets/biến môi trường.
- Cấu hình SePay Sandbox, IPN HTTPS public, Bank Webhook HMAC-SHA256 và tài khoản test.
- Tạo Tenant, Landlord, Invoice Issued/PartiallyPaid, JWT Tenant.

## Ma trận kiểm thử

| Mã | Tình huống | Kết quả mong đợi | Kết quả thực tế |
|---|---|---|---|
| SP-01 | Không JWT gọi checkout | 401 | Chưa chạy |
| SP-02 | Tenant không sở hữu Invoice gọi checkout | 404 | Chưa chạy |
| SP-03 | Invoice Draft/Cancelled/Paid | 409 | Chưa chạy |
| SP-04 | Invoice có Payment thủ công Pending | 409; không tạo Payment SePay | Chưa chạy |
| SP-05 | Invoice đã có Payment SePay Pending | Trả lại cùng PaymentId/orderInvoiceNumber; không tạo Payment/notification mới | Chưa chạy |
| SP-06 | Invoice còn dư nợ VND nguyên | Tạo một Payment Pending trên bảng Payments hiện có | Chưa chạy |
| SP-07 | Invoice có số dư thập phân | 400; không tạo checkout | Chưa chạy |
| SP-08 | Thiếu merchant/secret/URL config | 503; không lộ secret | Chưa chạy |
| SP-09 | Chữ ký checkout | HMAC-SHA256 Base64 theo đúng thứ tự field SePay | Chưa chạy |
| SP-10 | Submit form Sandbox | SePay nhận form và chuyển hướng checkout | Chưa chạy |
| SP-11 | Browser return success nhưng chưa có IPN | Payment vẫn Pending | Chưa chạy |
| SP-12 | IPN sai X-Secret-Key | 401; không đổi Payment | Chưa chạy |
| SP-13 | IPN timestamp cũ/quá xa tương lai | 401; không đổi Payment | Chưa chạy |
| SP-14 | IPN sai order code/PaymentId | 401; không đổi Payment | Chưa chạy |
| SP-15 | IPN amount/currency/order_status sai | 401; không đổi Payment/Invoice | Chưa chạy |
| SP-16 | IPN transaction_status không APPROVED | 401; không đổi Payment | Chưa chạy |
| SP-17 | IPN hợp lệ ORDER_PAID/CAPTURED/APPROVED | Payment Succeeded; Invoice status tính lại; lưu TransactionCode/PaidAt | Chưa chạy |
| SP-18 | Gửi lặp IPN cùng giao dịch | 200 ACK; không cập nhật/notification lần hai | Chưa chạy |
| SP-19 | IPN TRANSACTION_VOID khi Payment Pending | Payment Cancelled; Invoice status tính lại | Chưa chạy |
| SP-20 | Bank Webhook sai HMAC | 401; không đổi dữ liệu | Chưa chạy |
| SP-21 | Bank Webhook timestamp sai | 401; không đổi dữ liệu | Chưa chạy |
| SP-22 | Bank Webhook sai accountNumber | 401; không đổi dữ liệu | Chưa chạy |
| SP-23 | Bank Webhook transferType=out | 200 ACK; không ghi nhận thanh toán | Chưa chạy |
| SP-24 | Bank Webhook transferType=in, đúng code/amount | Payment Succeeded; Invoice status tính lại | Chưa chạy |
| SP-25 | Bank Webhook không có code của hệ thống | 200 ACK; không thay đổi Payment | Chưa chạy |
| SP-26 | Bank Webhook code đúng nhưng amount sai | 200 ACK; không thay đổi Payment; có log đối soát | Chưa chạy |
| SP-27 | Gửi lặp Bank Webhook/IPN | Không ghi nhận Payment/notification trùng | Chưa chạy |
| SP-28 | IPN và Bank Webhook cùng xác nhận một Payment | Payment chỉ Succeeded một lần | Chưa chạy |
| SP-29 | Payment SePay thành công | Tenant và Landlord nhận Payment notification đúng ReferenceId | Chưa chạy |
| SP-30 | Kiểm tra schema | Chỉ dùng AppDbContext/Payments hiện có; không có PaymentDbContext hoặc bảng Payments thứ hai | Chưa chạy |

## Kiểm tra mã thanh toán SePay
- Prefix cấu hình: `SP`.
- Suffix: 26 ký tự Base32 chữ/số.
- Trường `code` của Bank Webhook phải khớp mã `order_invoice_number`.
- Không ghi secret, JWT, số tài khoản thật hoặc dữ liệu giao dịch Production vào Git.

## Ghi nhận
- Ghi commit SHA, môi trường Sandbox, request, response, PaymentId, InvoiceId, NotificationId.
- Kiểm tra số dư hóa đơn và trạng thái Payment sau từng callback.
- Chỉ bật Production sau khi toàn bộ test Sandbox được xác nhận.
