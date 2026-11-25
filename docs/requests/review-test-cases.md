# Checklist kiểm thử đánh giá căn nhà

> Chỉ đánh dấu Đạt sau khi gọi API thật trên SQL Server test và ghi lại status code/response.

## Chuẩn bị

- Tenant A có hợp đồng Active đã hết hạn hoặc Completed với Property A.
- Tenant B không có hợp đồng với Property A.
- Một hợp đồng Active chưa hết hạn và một hợp đồng Draft/AwaitingSignature.
- Một hợp đồng đã được đánh giá.
- Tài khoản không có role Tenant.

## Ma trận kiểm thử

| Mã | Tình huống | Kết quả mong đợi | Kết quả thực tế |
|---|---|---|---|
| REV-01 | Người chưa đăng nhập xem đánh giá Property A | 200; chỉ trả rating, comment, createdAt; không lộ thông tin cá nhân | Chưa chạy |
| REV-02 | Property không tồn tại | 404 | Chưa chạy |
| REV-03 | page/pageSize không hợp lệ hoặc pageSize > 30 | 400 | Chưa chạy |
| REV-04 | Property chưa có đánh giá | averageRating = 0, totalReviews = 0, items rỗng | Chưa chạy |
| REV-05 | Tenant A xem GET /api/reviews/mine | Chỉ trả đánh giá của các hợp đồng thuộc Tenant A | Chưa chạy |
| REV-06 | Không có JWT gọi /mine hoặc POST | 401 | Chưa chạy |
| REV-07 | User không có role Tenant gọi /mine hoặc POST | 403 | Chưa chạy |
| REV-08 | Tenant B gửi đánh giá cho hợp đồng của Tenant A | 403 | Chưa chạy |
| REV-09 | Tenant A gửi đánh giá khi hợp đồng chưa kết thúc | 409 | Chưa chạy |
| REV-10 | Tenant A đánh giá hợp đồng Completed | 201; lưu một đánh giá | Chưa chạy |
| REV-11 | Tenant A đánh giá hợp đồng Active có EndDate <= ngày UTC+7 hiện tại | 201 | Chưa chạy |
| REV-12 | Rating = 0 hoặc 6 | 400; không lưu | Chưa chạy |
| REV-13 | Comment dài hơn 2.000 ký tự | 400 | Chưa chạy |
| REV-14 | Gửi đánh giá lần hai cho cùng hợp đồng | 409; unique index bảo vệ cả request đồng thời | Chưa chạy |
| REV-15 | Hai request đánh giá đồng thời cùng hợp đồng | Chỉ một bản ghi tồn tại; request còn lại trả lỗi xung đột phù hợp | Chưa chạy |
| REV-16 | Trang chi tiết Property A | Hiển thị điểm trung bình, số lượt và các đánh giá mới nhất | Chưa chạy |
| REV-17 | Hồ sơ người thuê | Hiển thị lịch sử đánh giá do người đó gửi; không hiển thị với role khác | Chưa chạy |

## Ghi nhận

- Ghi commit SHA, môi trường, tài khoản/role và status code thực tế.
- Không commit JWT, mật khẩu, connection string hoặc dữ liệu cá nhân thật.
- Đánh giá chỉ được tạo bởi người thuê có hợp đồng thực tế và sau khi thời hạn thuê kết thúc.
