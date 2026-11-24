# Tích hợp SePay cho hệ thống thuê nhà

Tích hợp SePay dùng chung `AppDbContext`, `Invoice` và `Payment` hiện có. Không tạo `PaymentDbContext`, entity thanh toán riêng hoặc bảng `Payments` thứ hai.

## Luồng thanh toán Gateway

1. Tenant gọi `POST /api/sepay/invoices/{invoiceId}/checkout` bằng JWT.
2. Server lấy TenantId từ Claims, kiểm tra Tenant sở hữu hóa đơn và tính số tiền còn phải thanh toán.
3. Server tạo Payment Pending trong bảng Payments hiện có, sinh mã thanh toán duy nhất dạng `TN########`, tạo chữ ký HMAC-SHA256.
4. Client nhận `checkoutUrl` và `formFields`, tạo HTML form method POST đến checkoutUrl.
5. SePay gọi `POST /api/sepay/ipn`. Chỉ IPN có X-Secret-Key hợp lệ, timestamp hợp lệ, order CAPTURED và transaction APPROVED, đúng invoice/amount/VND mới cập nhật Payment và Invoice.
6. URL return chỉ phục vụ điều hướng giao diện, tuyệt đối không xác nhận thanh toán.

## Luồng Bank Webhook

1. Tenant tạo checkout và dùng mã thanh toán trong form/chuyển khoản.
2. Cấu hình SePay Webhook nhận tiền vào (In_only), chọn HMAC-SHA256 và URL `https://<domain>/api/sepay/bank-webhook`.
3. Cấu hình Payment Code trên SePay với tiền tố `TN` và hậu tố 8 chữ số để SePay đưa mã vào trường `code`.
4. Server xác minh chữ ký trên raw body theo công thức HMAC-SHA256 của `{timestamp}.{raw_body}`, kiểm tra timestamp ±5 phút và số tài khoản nhận.
5. Chỉ giao dịch tiền vào có mã thanh toán và số tiền khớp Payment Pending mới được ghi nhận.
6. Webhook trả HTTP 200 với JSON `{"success": true}` khi đã xác thực và tiếp nhận hợp lệ. Giao dịch không khớp được ACK nhưng không thay đổi Payment.

## Cấu hình bí mật

Không commit secret vào Git. Dùng User Secrets ở máy phát triển hoặc environment variables ở máy chủ:

- `SePay:MerchantId`
- `SePay:SecretKey`
- `SePay:IpnSecret`
- `SePay:WebhookSecret`
- `SePay:PublicUrl` (bắt buộc HTTPS công khai)
- `SePay:BankAccountNumber`

Ví dụ (thay bằng giá trị thật ở máy local, không đưa giá trị thật vào file):

```bash
dotnet user-secrets set "SePay:MerchantId" "<MERCHANT_ID>"
dotnet user-secrets set "SePay:SecretKey" "<MERCHANT_SECRET>"
dotnet user-secrets set "SePay:IpnSecret" "<IPN_SECRET>"
dotnet user-secrets set "SePay:WebhookSecret" "<WEBHOOK_HMAC_SECRET>"
dotnet user-secrets set "SePay:PublicUrl" "https://<public-domain>"
dotnet user-secrets set "SePay:BankAccountNumber" "<BANK_ACCOUNT_NUMBER>"
```

Environment variables tương ứng dùng dấu gạch dưới kép, ví dụ `SePay__MerchantId`.

## Endpoint

| Method | Endpoint | Quyền |
|---|---|---|
| POST | `/api/sepay/invoices/{invoiceId}/checkout` | Tenant |
| POST | `/api/sepay/ipn` | SePay IPN + X-Secret-Key |
| POST | `/api/sepay/bank-webhook` | SePay HMAC-SHA256 |
| GET | `/api/sepay/return/{success\|error\|cancel}` | Public, chỉ điều hướng |

## Kiểm thử trước khi Production

- Chạy build .NET 10 và test trên SQL Server.
- Dùng SePay Sandbox/Test mode, không dùng giao dịch thật để thử ban đầu.
- Kiểm tra chữ ký đúng/sai, timestamp quá hạn, amount sai, currency sai, order status sai.
- Kiểm tra webhook tiền vào/tiền ra, sai tài khoản, sai mã thanh toán, sai số tiền.
- Replay cùng IPN/webhook nhiều lần: Payment chỉ được cập nhật một lần.
- Kiểm tra payment vượt số dư hóa đơn, người thuê không sở hữu hóa đơn và request không có JWT.
- Cấu hình HTTPS công khai, đồng bộ NTP, backup SQL Server và theo dõi log webhook.
- Không sử dụng lại secret từng được chia sẻ; thu hồi và tạo mới trước khi bật Live.

Tài liệu chính thức:
- https://developer.sepay.vn/vi/cong-thanh-toan/API/don-hang/form-thanh-toan
- https://developer.sepay.vn/vi/cong-thanh-toan/IPN
- https://developer.sepay.vn/vi/sepay-webhooks/xac-thuc
- https://developer.sepay.vn/vi/sepay-webhooks/tich-hop-webhook
