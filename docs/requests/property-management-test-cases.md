# Checklist kiểm thử API quản lý nhà

Dùng cùng môi trường đã cấu hình SQL Server và JWT. Các endpoint ghi cần JWT của tài khoản có role `Landlord`. Không dùng tài khoản Tenant để kết luận API ghi lỗi phân quyền.

## A. Danh sách, tìm kiếm và phân trang

| Mã | Thao tác | Kết quả mong đợi |
|---|---|---|
| A01 | `GET /api/properties` | 200; chỉ có tin Published và Approved |
| A02 | Tìm bằng `keyword` | 200; kết quả khớp tiêu đề/địa chỉ/tỉnh/phường |
| A03 | Lọc `province`, `propertyTypeId` | 200; chỉ trả kết quả thỏa bộ lọc |
| A04 | Lọc min/max giá, diện tích, số phòng ngủ | 200; kết quả nằm trong giới hạn |
| A05 | `page=0`, `pageSize=0`, `pageSize=51` | 400 |
| A06 | min lớn hơn max | 400 |
| A07 | Không có kết quả | 200; Items rỗng, TotalItems = 0 |

## B. Chi tiết và quyền truy cập

| Mã | Thao tác | Kết quả mong đợi |
|---|---|---|
| B01 | Xem tin Published và Approved | 200 |
| B02 | Xem tin Draft/Hidden/Closed bằng tài khoản thường | 404 |
| B03 | Chủ nhà xem tin của mình | 200 |
| B04 | Chủ nhà khác xem tin chưa công khai | 404 |
| B05 | ID không tồn tại | 404 |

## C. Tạo và cập nhật

| Mã | Thao tác | Kết quả mong đợi |
|---|---|---|
| C01 | Không gửi JWT khi tạo | 401 |
| C02 | Gửi JWT role Tenant khi tạo | 403 |
| C03 | Tạo với dữ liệu hợp lệ, loại nhà có tồn tại | 201; LandlordId lấy từ JWT; trạng thái Draft/Pending |
| C04 | Tạo với PropertyTypeId không tồn tại | 400 |
| C05 | Tạo với tiêu đề/giá/diện tích không hợp lệ | 400 |
| C06 | Chủ nhà cập nhật tin của mình | 200; trạng thái chuyển về Draft/Pending |
| C07 | Chủ nhà khác cập nhật tin | 403 |
| C08 | Cập nhật ID không tồn tại | 404 |

## D. Trạng thái tin

| Mã | Thao tác | Kết quả mong đợi |
|---|---|---|
| D01 | Chủ nhà ẩn tin của mình | 204 |
| D02 | Chủ nhà đóng tin của mình | 204 |
| D03 | Chủ nhà đăng tin chưa được duyệt | 409 |
| D04 | Chủ nhà đăng tin đã Approved | 204 |
| D05 | Chủ nhà khác đổi trạng thái | 403 |
| D06 | Gửi giá trị enum không hợp lệ hoặc Draft | 400 |

## E. Hình ảnh

| Mã | Thao tác | Kết quả mong đợi |
|---|---|---|
| E01 | Thêm URL HTTPS hợp lệ | 201 |
| E02 | Thêm URL HTTP hoặc URL sai định dạng | 400 |
| E03 | Thêm ảnh cho tin của chủ nhà khác | 403 |
| E04 | Thêm ảnh thứ 21 | 409 |
| E05 | Đặt ảnh thuộc tin làm ảnh chính | 204 |
| E06 | Đặt ảnh không thuộc tin làm ảnh chính | 404 |
| E07 | Xóa ảnh | 204 |
| E08 | Xóa ảnh chính khi còn ảnh khác | 204; ảnh có DisplayOrder nhỏ nhất còn lại được đặt làm ảnh chính |
| E09 | Xóa ảnh không tồn tại | 404 |

## Ghi nhận kết quả

Khi chạy, ghi lại:
- Commit SHA được kiểm thử.
- Môi trường và phiên bản .NET SDK.
- Kết quả `dotnet restore` và `dotnet build`.
- Kết quả từng nhóm A–E; kèm response/status code nếu thất bại.

Checklist này là kịch bản cần chạy, không phải bằng chứng các ca kiểm thử đã chạy thành công.
