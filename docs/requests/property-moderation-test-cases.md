# Checklist kiểm thử API Admin kiểm duyệt tin đăng

> Đây là kế hoạch kiểm thử. Chỉ đánh dấu Passed sau khi chạy thật trên SQL Server và ghi status code/kết quả thực tế.

| Mã | Tình huống | Kết quả mong đợi | Kết quả thực tế |
|---|---|---|---|
| MOD-01 | Admin gọi danh sách không truyền query | 200; mặc định lọc Pending, metadata phân trang hợp lệ | Chưa chạy |
| MOD-02 | Admin lọc status=Rejected | 200; chỉ trả tin Rejected | Chưa chạy |
| MOD-03 | page=0, pageSize=0 hoặc pageSize=51 | 400 | Chưa chạy |
| MOD-04 | status enum không tồn tại | 400 | Chưa chạy |
| MOD-05 | Trang vượt totalPages | 200; items rỗng, metadata hợp lệ | Chưa chạy |
| MOD-06 | Admin xem chi tiết tin tồn tại | 200; có hình ảnh, tiện ích và lịch sử kiểm duyệt | Chưa chạy |
| MOD-07 | Admin xem ID không tồn tại | 404 | Chưa chạy |
| MOD-08 | Admin duyệt tin Pending | 200; ModerationStatus=Approved; có log Approved với AdminId/PropertyId/CreatedAt | Chưa chạy |
| MOD-09 | Admin duyệt lần hai hoặc duyệt tin không Pending | 409; không tạo log mới | Chưa chạy |
| MOD-10 | Admin từ chối tin Pending với lý do hợp lệ | 200; ModerationStatus=Rejected, ListingStatus=Draft; log Rejected có lý do | Chưa chạy |
| MOD-11 | Từ chối thiếu/lý do dưới 5 ký tự | 400; không đổi trạng thái, không tạo log | Chưa chạy |
| MOD-12 | Từ chối tin đã Approved/Rejected | 409; không tạo log mới | Chưa chạy |
| MOD-13 | Admin ẩn tin Approved + Published với lý do | 200; ListingStatus=Hidden; log Hidden | Chưa chạy |
| MOD-14 | Ẩn tin đã Hidden, Draft, Closed hoặc chưa Approved | 409; không tạo log mới | Chưa chạy |
| MOD-15 | Admin khôi phục tin Approved + Hidden với lý do | 200; ListingStatus=Published; log Restored | Chưa chạy |
| MOD-16 | Khôi phục tin không Hidden hoặc chưa Approved | 409; không tạo log mới | Chưa chạy |
| MOD-17 | Landlord gọi bất kỳ endpoint kiểm duyệt nào | 403 | Chưa chạy |
| MOD-18 | Tenant gọi bất kỳ endpoint kiểm duyệt nào | 403 | Chưa chạy |
| MOD-19 | Không có JWT hoặc JWT không hợp lệ | 401 | Chưa chạy |
| MOD-20 | Kiểm tra tin Rejected/Pending trong API công khai | Không được xuất hiện công khai | Chưa chạy |
| MOD-21 | Kiểm tra tin Hidden trong API công khai | Không được xuất hiện công khai | Chưa chạy |
| MOD-22 | Hai request duyệt/từ chối lặp tuần tự | Request đầu thành công; request sau 409 và không phát sinh log trùng | Chưa chạy |

## Quy tắc nghiệp vụ hiện thực

- Chỉ role Admin được truy cập controller kiểm duyệt.
- AdminId lấy từ JWT Claim `ClaimTypes.NameIdentifier`, không nhận từ request.
- Duyệt/từ chối chỉ áp dụng cho ModerationStatus=Pending.
- Từ chối chuyển ListingStatus về Draft để tin không còn ở trạng thái công khai.
- Ẩn chỉ áp dụng cho tin Approved + Published; khôi phục chỉ áp dụng cho Approved + Hidden.
- Mỗi lần chuyển trạng thái hợp lệ tạo một bản ghi PropertyModerationLogs trong cùng lần SaveChanges.
- API danh sách lọc trước CountAsync và phân trang server-side bằng OrderBy/Skip/Take.
