# Tích hợp SePay – Production

Module này được bổ sung cho ASP.NET Core Web API .NET 10. Hai kênh nhận thanh toán được tách riêng:

- Payment Gateway: POST /api/sepay/checkout, nhận IPN tại POST /api/sepay/ipn.
- Bank Webhook: nhận biến động tài khoản tại POST /api/sepay/bank-webhook.

## Cấu hình môi trường

Không commit bí mật vào Git. Cấu hình bằng biến môi trường trên máy chủ:

- ConnectionStrings__Payments hoặc PAYMENTS_DB_CONNECTION: SQL Server connection string.
- SEPAY_MERCHANT_ID_PRODUCTION: Merchant ID Production.
- SEPAY_SECRET_KEY_PRODUCTION: Merchant Secret Key dùng ký form checkout.
- SEPAY_IPN_SECRET_PRODUCTION: Secret Key cấu hình cho IPN Gateway (header X-Secret-Key).
- SEPAY_WEBHOOK_SECRET_PRODUCTION: Secret riêng cho webhook ngân hàng, chọn HMAC-SHA256 trong trang cấu hình Webhooks SePay.
- SEPAY_PUBLIC_URL: domain HTTPS công khai, ví dụ https://api.example.com.
- SEPAY_CHECKOUT_API_KEY: khóa nội bộ để gọi API checkout/tra cứu; không đặt trong frontend trình duyệt.
- SEPAY_BANK_ACCOUNT_NUMBER_PRODUCTION: số tài khoản nhận tiền phải khớp payload webhook.

Các khóa Gateway, IPN và Bank Webhook là ba mục đích khác nhau; không dùng chung một secret. Nếu secret đã được chia sẻ, thu hồi và tạo khóa mới trước khi vận hành.

## Database

Chạy database/sepay-payments.sql trên SQL Server. Tạo database và user có quyền tối thiểu cần thiết trước khi cấu hình connection string.

## Gateway

1. Cấu hình IPN URL trên SePay: https://<domain>/api/sepay/ipn.
2. Chọn kiểu xác thực SECRET_KEY và đặt secret trùng SEPAY_IPN_SECRET_PRODUCTION.
3. Backend tạo invoice duy nhất, lưu trạng thái Pending, ký các trường theo đúng thứ tự tài liệu SePay.
4. Client nhận checkoutUrl và formFields từ POST /api/sepay/checkout, tạo form POST để submit tới checkoutUrl.
5. Chỉ IPN hợp lệ với ORDER_PAID, order_status CAPTURED, invoice và amount trùng database mới đánh dấu Paid. URL return không xác nhận thanh toán.

## Bank Webhook

1. Tạo webhook nhận tiền vào (In_only).
2. Chọn HMAC-SHA256. Secret lưu ở SEPAY_WEBHOOK_SECRET_PRODUCTION.
3. Cấu hình URL https://<domain>/api/sepay/bank-webhook.
4. Backend xác minh X-SePay-Signature theo HMAC-SHA256 của chuỗi timestamp + "." + raw body; từ chối timestamp quá 5 phút.
5. Chỉ giao dịch tiền vào, đúng số tài khoản đã cấu hình, có mã invoice và đúng số tiền mới được đánh dấu Paid. EventKey unique dùng chống xử lý trùng.

## API

### Tạo checkout

POST /api/sepay/checkout

Gửi header `X-Payment-Api-Key: <SEPAY_CHECKOUT_API_KEY>`. Chỉ gọi endpoint này từ backend tin cậy; không nhúng khóa vào ứng dụng trình duyệt/mobile.

```json
{
  "amount": 1500000,
  "description": "Thanh toan tien coc thue nha",
  "customerId": "USER-001"
}
```

Response gồm invoiceNumber, checkoutUrl và formFields. Frontend phải submit form method POST với toàn bộ formFields tới checkoutUrl.

### Tra cứu thanh toán

GET /api/sepay/payments/{invoiceNumber}

Cũng yêu cầu header `X-Payment-Api-Key`.

Trạng thái: Pending, Paid, Cancelled.

## Kiểm thử và triển khai

- Dùng tài khoản/test mode của SePay để xác minh form checkout và webhook trước khi bật nhận tiền thật.
- Production yêu cầu HTTPS, đồng bộ thời gian NTP, SQL Server backup và giám sát webhook.
- Kiểm tra lại payload thực tế của merchant với tài liệu SePay trước khi bật production.
- API hiện không gắn payment với entity Booking/Lease vì repository tại thời điểm tích hợp chưa có mô hình đặt thuê.
- Endpoint checkout/tra cứu hiện dùng khóa nội bộ X-Payment-Api-Key. Trước khi tích hợp frontend, thay cơ chế này bằng authentication/authorization theo người dùng và ràng buộc invoice với booking.
