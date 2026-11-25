# Tài khoản và dữ liệu demo NhàỞ

Dữ liệu demo được tạo tự động khi chạy backend ở môi trường `Development` với cấu hình `SeedDemoData: true`.

Seeder sử dụng ID cố định cho từng bản ghi và chỉ thêm những bản ghi còn thiếu. Vì vậy, có thể chạy lại để mở rộng dữ liệu mà không nhân đôi các bản ghi demo đã có.

## Tài khoản đăng nhập

Mật khẩu dùng chung cho tất cả tài khoản demo:

```text
NhaO@123456
```

| Vai trò | Số lượng | Email |
|---|---:|---|
| Quản trị viên | 10 | `admin@nhao.test`, `admin02@nhao.test` đến `admin10@nhao.test` |
| Chủ nhà | 50 | `chunha01@nhao.test` đến `chunha50@nhao.test` |
| Người thuê | 180 | `nguoithue01@nhao.test` đến `nguoithue180@nhao.test` |

Tất cả là tài khoản giả lập, không phải người dùng thật. Tài khoản đầu tiên của mỗi vai trò lần lượt là `admin@nhao.test`, `chunha01@nhao.test`, `nguoithue01@nhao.test`.

## Dữ liệu mục tiêu

| Loại dữ liệu | Số lượng |
|---|---:|
| Tài khoản | 240 |
| Tin đăng nhà/phòng | 450 |
| Yêu cầu thuê | 360 |
| Hợp đồng | 150 |
| Hóa đơn | 450 |
| Hội thoại | 240 |
| Tin nhắn | 1.920 |
| Nhà yêu thích | 720 |
| Thông báo | 1.200 |
| Nhật ký kiểm duyệt | 120 |
| Đánh giá | 20 |
| Giao dịch thanh toán demo | Thay đổi theo trạng thái hóa đơn |

Tin đăng, yêu cầu thuê, hợp đồng, hóa đơn và giao dịch thanh toán được phân bổ nhiều trạng thái để thử bộ lọc, phân trang và dashboard.

## Hình ảnh

Seeder không tạo bản ghi ảnh giả. Các tin đăng mẫu để trống hình ảnh để có thể tự bổ sung ảnh sau.

## Cách chạy

Tại thư mục backend:

```powershell
dotnet run
```

Khi môi trường là `Development`, backend tự chạy migration còn thiếu và bổ sung các bản ghi demo chưa tồn tại.

## An toàn

- Seeder chỉ được gọi khi `IWebHostEnvironment.IsDevelopment()` và `SeedDemoData` bật.
- `appsettings.Development.json` bật cờ này cho máy phát triển.
- Production không tự chạy migration hoặc seed dữ liệu demo.
- Seeder có transaction và ID cố định để có thể chạy lại mà không thêm trùng bản ghi demo.
- Không dùng tài khoản demo trên môi trường công khai.
