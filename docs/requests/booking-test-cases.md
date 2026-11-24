# Checklist kiểm thử API đặt thuê

Tài liệu này dùng cùng `Bookings.http` trên môi trường Development có SQL Server và dữ liệu thử nghiệm riêng.

> **Trạng thái thực thi:** Chưa chạy trên SQL Server trong môi trường thao tác hiện tại. Không đánh dấu đạt cho đến khi gửi request thật và ghi status code nhận được.

## Chuẩn bị

1. Cấu hình `ConnectionStrings:DefaultConnection` và `Jwt:Key` bằng User Secrets theo README.
2. Chạy `dotnet ef database update`, sau đó chạy API ở HTTPS `https://localhost:7295`.
3. Chuẩn bị dữ liệu thử nghiệm:
   - Một Landlord sở hữu ít nhất hai Property: một căn Approved + Published và một căn chưa được đăng.
   - Hai Tenant khác nhau.
   - Một Landlord khác không sở hữu các Property trên.
4. Đăng nhập từng tài khoản, điền JWT và GUID vào biến trong `Bookings.http`.

## Ma trận kiểm thử

| Nhóm | Ca kiểm thử | Kết quả mong đợi | Kết quả thực tế |
|---|---|---|---|
| Tạo | Tenant tạo yêu cầu với ngày hợp lệ, Property Approved + Published | 201, Status = Pending | Chưa chạy |
| Tạo | Không có JWT hoặc JWT không hợp lệ | 401 | Chưa chạy |
| Tạo | Landlord gọi API tạo yêu cầu | 403 | Chưa chạy |
| Tạo | Ngày bắt đầu trong quá khứ | 400 | Chưa chạy |
| Tạo | Ngày kết thúc trước hoặc bằng ngày bắt đầu | 400 | Chưa chạy |
| Tạo | Số người = 0 hoặc ngoài giới hạn DTO | 400 | Chưa chạy |
| Tạo | Property không tồn tại | 404 | Chưa chạy |
| Tạo | Tenant tự gửi yêu cầu thuê Property của mình | 400 | Chưa chạy |
| Tạo | Property chưa Approved hoặc chưa Published | 409 | Chưa chạy |
| Tạo | Khoảng ngày trùng với Booking Accepted | 409 | Chưa chạy |
| Danh sách | Tenant xem `GET /api/bookings/mine` | 200, chỉ có Booking của Tenant đó | Chưa chạy |
| Danh sách | Tenant gọi `GET /api/bookings/incoming` | 403 | Chưa chạy |
| Danh sách | Landlord xem `GET /api/bookings/incoming` | 200, chỉ có Booking thuộc Property mình sở hữu | Chưa chạy |
| Chi tiết | Tenant sở hữu Booking xem chi tiết | 200 | Chưa chạy |
| Chi tiết | Tenant khác xem Booking không thuộc mình | 403 | Chưa chạy |
| Chi tiết | Landlord sở hữu Property xem Booking | 200 | Chưa chạy |
| Chi tiết | Landlord khác xem Booking | 403 | Chưa chạy |
| Chấp nhận | Landlord sở hữu Property chấp nhận Booking Pending | 200, Status = Accepted | Chưa chạy |
| Chấp nhận | Landlord không sở hữu Property chấp nhận Booking | 403 | Chưa chạy |
| Chấp nhận | Chấp nhận Booking không còn Pending | 409 | Chưa chạy |
| Từ chối | Landlord sở hữu Property từ chối Booking Pending | 200, Status = Rejected | Chưa chạy |
| Từ chối | Landlord không sở hữu Property từ chối Booking | 403 | Chưa chạy |
| Từ chối | Từ chối Booking không còn Pending | 409 | Chưa chạy |
| Hủy | Tenant sở hữu Booking Pending hủy yêu cầu | 200, Status = Cancelled | Chưa chạy |
| Hủy | Tenant khác hủy Booking | 403 | Chưa chạy |
| Hủy | Hủy Booking đã Accepted/Rejected/Cancelled | 409 | Chưa chạy |
| Trùng lịch | Hai Booking Pending trùng lịch; lần lượt chấp nhận | Chỉ một Booking được Accepted; lần còn lại 409 | Chưa chạy |
| Trùng lịch | Hai request chấp nhận Booking trùng lịch gửi gần đồng thời | Không thể có cả hai cùng Accepted; ghi lại status của cả hai request | Chưa chạy |

## Ghi nhận khi chạy

Với mỗi ca, ghi lại:
- Thời điểm chạy và tài khoản/role dùng để gọi.
- Request (ẩn JWT và thông tin nhạy cảm).
- HTTP status code.
- Status/response body thực tế.
- Đạt/không đạt; nếu lỗi, cách tái hiện và log liên quan.

Không đưa JWT, mật khẩu, connection string hoặc dữ liệu cá nhân thật vào GitHub.
