# Tài khoản và dữ liệu demo NhàỞ

Dữ liệu demo được tạo tự động khi chạy backend ở môi trường `Development` với cấu hình `SeedDemoData: true`.

## Tài khoản đăng nhập

Mật khẩu dùng chung cho tất cả tài khoản demo:

```text
NhaO@123456
```

| Vai trò | Email |
|---|---|
| Quản trị viên | `admin@nhao.test` |
| Chủ nhà 1–5 | `chunha01@nhao.test` đến `chunha05@nhao.test` |
| Người thuê 1–18 | `nguoithue01@nhao.test` đến `nguoithue18@nhao.test` |

Tất cả là tài khoản giả lập, không phải người dùng thật.

## Dữ liệu được tạo

| Loại dữ liệu | Số lượng dự kiến |
|---|---:|
| Tài khoản | 24 |
| Tin đăng nhà/phòng | 45 |
| Yêu cầu thuê | 36 |
| Hợp đồng | 15 |
| Hóa đơn | 45 |
| Giao dịch thanh toán demo | Thay đổi theo trạng thái hóa đơn |
| Hội thoại | 24 |
| Tin nhắn | 192 |
| Nhà yêu thích | 72 |
| Thông báo | 120 |
| Nhật ký kiểm duyệt | 12 |
| Đánh giá | 2 |

Tin đăng được phân bổ nhiều trạng thái để thử bộ lọc và màn hình kiểm duyệt. Yêu cầu thuê, hợp đồng, hóa đơn và thanh toán cũng có nhiều trạng thái khác nhau.

## Hình ảnh

Seeder không tạo bản ghi ảnh giả. Các tin đăng mẫu để trống hình ảnh để có thể tự tải ảnh thật lên sau.

## Cách chạy

Tại thư mục backend:

```powershell
dotnet run
```

Khi môi trường là `Development`, backend tự chạy migration còn thiếu và seed dữ liệu demo nếu chưa có tài khoản đánh dấu `admin@nhao.test`.

## An toàn

- Seeder chỉ được gọi khi `IWebHostEnvironment.IsDevelopment()` và `SeedDemoData` bật.
- `appsettings.Development.json` bật cờ này cho máy phát triển.
- Production không tự chạy migration hoặc seed dữ liệu demo.
- Seeder có transaction và kiểm tra tài khoản đánh dấu để tránh thêm trùng ở các lần khởi động sau.
- Không dùng tài khoản demo trên môi trường công khai.
