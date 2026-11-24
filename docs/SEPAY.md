# Tích hợp SePay với hóa đơn thuê nhà

SePay được tích hợp trực tiếp vào mô hình thanh toán hiện có của dự án:

- Dùng `AppDbContext`, `Invoice`, `InvoiceItem` và `Payment`.
- Không tạo `PaymentDbContext`, bảng `Payments` mới hoặc bảng log webhook riêng.
- Payment do SePay tạo có `PaymentProvider=SePay`, `PaymentMethod=BankTransfer`, `Status=Pending`; IPN/Bank Webhook hợp lệ mới chuyển sang `Succeeded`.
- `Payment.TransactionCode` lưu mã giao dịch nhà cung cấp và có unique index hiện có để chống ghi nhận trùng.

## Cấu hình môi trường

Không commit secret hoặc connection string. Dùng User Secrets ở local và biến môi trường ở server:

| Key | Ý nghĩa |
|---|---|
| `SEPAY_MERCHANT_ID` | Merchant ID |
| `SEPAY_SECRET_KEY` | Secret Key dùng ký form checkout |
| `SEPAY_CHECKOUT_INIT_URL` | URL submit form checkout |
| `SEPAY_PUBLIC_URL` | Domain HTTPS công khai của API |
| `SEPAY_IPN_SECRET` | Secret cấu hình riêng cho IPN Gateway, header `X-Secret-Key` |
| `SEPAY_WEBHOOK_SECRET` | Secret HMAC-SHA256 riêng cho Bank Webhook |
| `SEPAY_BANK_ACCOUNT_NUMBER` | Số tài khoản nhận tiền cần khớp payload webhook |

URL checkout theo môi trường:

- Sandbox: `https://pay-sandbox.sepay.vn/v1/checkout/init`
- Production: `https://pay.sepay.vn/v1/checkout/init`

Dùng sandbox trước. Chỉ chuyển Production sau khi merchant, IPN và webhook đã được kiểm thử thật.

## Luồng checkout

1. Tenant gọi `POST /api/invoices/{invoiceId}/sepay-checkout` bằng JWT.
2. Backend xác minh Tenant sở hữu hóa đơn, tính số dư từ các Payment đã Succeeded và tạo Payment Pending trong bảng `Payments` hiện có.
3. Backend trả `checkoutUrl`, `orderInvoiceNumber` và danh sách `formFields` theo đúng thứ tự ký.
4. Frontend tạo HTML form POST đến `checkoutUrl`, giữ nguyên thứ tự `formFields`; không tự tạo/chỉnh signature.
5. SePay chuyển người dùng về success/error/cancel URL. URL return chỉ hiển thị kết quả, không được dùng để đánh dấu đã thanh toán.

### Mã thanh toán

`order_invoice_number` dùng mã `SP` + 26 ký tự Base32 mã hóa PaymentId. Mã này duy nhất và nằm trong giới hạn tiền tố/hậu tố alphanumeric. Khi cấu hình mã thanh toán trên SePay, đặt prefix `SP`, suffix 26 ký tự chữ/số để Bank Webhook trả lại trường `code` tương ứng.

SePay chỉ nhận VND nguyên. Backend không cho tạo checkout nếu số dư còn lại có phần thập phân.

## IPN Gateway

Cấu hình IPN URL:

`https://<domain>/api/sepay/ipn`

SePay gửi header `X-Secret-Key`. Backend xác minh secret, timestamp trong body (hợp lệ và không nằm quá xa trong tương lai; vẫn chấp nhận IPN cũ được gửi lại khi SePay retry), `notification_type=ORDER_PAID`, `order_status=CAPTURED`, `transaction_status=APPROVED`, currency VND, mã Payment và số tiền khớp database.

Chỉ IPN hợp lệ mới cập nhật Payment/Invoice. IPN gửi lại cho Payment đã Succeeded được ACK idempotent và không tạo notification lần nữa.

## Bank Webhook

Cấu hình webhook SePay:

- Event: tiền vào (`In_only`).
- Authentication: HMAC-SHA256.
- URL: `https://<domain>/api/sepay/bank-webhook`.
- Header chữ ký: `X-SePay-Signature: sha256={hex_hash}`.
- Header thời gian: `X-SePay-Timestamp`.

Chữ ký được xác minh trên raw body theo công thức HMAC-SHA256 của chuỗi `{timestamp}.{raw_body}`; không parse rồi serialize lại trước khi xác minh. Timestamp phải nằm trong ±5 phút. Backend chỉ ghi nhận giao dịch tiền vào đúng tài khoản, đúng mã Payment và đúng số tiền.

Bank Webhook không tạo thêm bảng event. Payment status và unique `TransactionCode` hiện có bảo vệ việc ghi nhận trùng. Giao dịch không khớp mã hoặc số tiền được bỏ qua và ghi log để đối soát thủ công.

Trước Production, cấu hình whitelist IP SePay tại firewall/reverse proxy theo danh sách IP chính thức; không chỉ dựa vào secret. SePay khuyến nghị đối soát định kỳ 15–30 phút qua API giao dịch để phát hiện webhook bị bỏ lỡ. PR này chưa triển khai tác vụ đối soát tự động; cần lên lịch như một hạng mục vận hành riêng. Không ghi raw payload có dữ liệu tài khoản vào log ứng dụng; dùng Delivery Logs của SePay để tra payload, và chỉ bổ sung lưu trữ audit nếu nhóm duyệt phạm vi/schema riêng.

## API

### Tạo checkout

`POST /api/invoices/{invoiceId}/sepay-checkout`

Role: Tenant. Backend chỉ cho phép hóa đơn thuộc Tenant và đang ở trạng thái Issued/PartiallyPaid/Overdue. Nếu có một Payment khác đang Pending thì yêu cầu xử lý Payment đó trước; nếu đã có SePay Payment Pending thì trả lại checkout của Payment hiện tại, không tạo Payment/notification mới.

Response gồm:

- `paymentId`
- `orderInvoiceNumber`
- `checkoutUrl`
- `formFields`: danh sách `{ name, value }` theo thứ tự cần submit.

### IPN Gateway

`POST /api/sepay/ipn`

Anonymous callback; bảo vệ bằng `X-Secret-Key`.

### Bank Webhook

`POST /api/sepay/bank-webhook`

Anonymous callback; bảo vệ bằng HMAC-SHA256 và timestamp.

### Return URL

`GET /api/sepay/return/{success|error|cancel}`

Chỉ hiển thị thông báo; không xác nhận thanh toán.

## Kiểm thử và vận hành

- Chạy migration hiện tại trước khi kiểm thử; PR SePay không yêu cầu migration hoặc bảng mới.
- Dùng SePay Sandbox, domain HTTPS public/tunnel và tài khoản test.
- Gửi thử IPN và Bank Webhook; kiểm tra đúng PaymentId, InvoiceId, amount, status, TransactionCode và Notification.
- Thử gửi lặp IPN/webhook; chỉ một lần được chuyển Payment sang Succeeded.
- Không đánh dấu test Production/SQL Server là Passed nếu chưa chạy giao dịch test thật.
- Nếu secret đã từng bị chia sẻ, thu hồi và tạo secret mới trước khi bật Production.
