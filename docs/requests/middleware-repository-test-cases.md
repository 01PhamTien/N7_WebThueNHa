# Checklist kiểm thử Middleware và Repository

> Chưa chạy trong môi trường SQL Server/API tại thời điểm tạo tài liệu. Chỉ đánh dấu Passed sau khi chạy thật và ghi status code/kết quả thực tế.

## A. ExceptionHandlingMiddleware

| Mã | Tình huống | Cách kiểm tra | Kết quả mong đợi | Kết quả thực tế |
|---|---|---|---|---|
| MW-01 | Lỗi validation | Gửi request có DTO sai validation hoặc ném `ValidationException` trong test | 400, `application/problem+json`, có `traceId` | Chưa chạy |
| MW-02 | Lỗi không tìm thấy | Trong test middleware, ném `KeyNotFoundException` | 404, ProblemDetails, có `traceId` | Chưa chạy |
| MW-03 | Lỗi xung đột cập nhật | Trong test middleware, ném `DbUpdateConcurrencyException` | 409, ProblemDetails, có `traceId` | Chưa chạy |
| MW-04 | Lỗi không dự kiến | Trong test middleware, ném `InvalidOperationException` | 500, thông báo an toàn | Chưa chạy |
| MW-05 | Production không lộ nội bộ | Chạy MW-04 với môi trường Production | Không có stack trace, SQL, connection string hoặc secret trong response/log | Chưa chạy |
| MW-06 | Development | Chạy MW-04 với môi trường Development | Response có thông tin phục vụ debug; response vẫn có `traceId` | Chưa chạy |
| MW-07 | Client hủy request | Hủy request khi action đang chạy | Middleware không cố ghi response lỗi sau khi client ngắt kết nối | Chưa chạy |

Có thể kiểm tra MW-01..04 bằng unit/integration test middleware; không thêm endpoint debug gây lỗi vào ứng dụng thật.

## B. Repository và giữ nguyên API

| Mã | Tình huống | Cách kiểm tra | Kết quả mong đợi | Kết quả thực tế |
|---|---|---|---|---|
| RP-01 | Danh sách nhà công khai | GET `/api/properties?page=1&pageSize=10` | Chỉ Approved + Published; metadata phân trang không đổi | Chưa chạy |
| RP-02 | Danh sách nhà của chủ nhà | GET `/api/properties/mine?page=1&pageSize=10` bằng Landlord | Chỉ trả dữ liệu thuộc UserId trong JWT | Chưa chạy |
| RP-03 | Chi tiết nhà | GET `/api/properties/{id}` | Response và quy tắc ẩn tin giữ nguyên | Chưa chạy |
| RP-04 | Danh sách yêu thích | GET `/api/favorites?page=1&pageSize=20` bằng Tenant/Landlord | Chỉ tin Approved + Published; phân trang server-side | Chưa chạy |
| RP-05 | Thêm yêu thích | POST `/api/favorites/{propertyId}` | Request đầu 201; request lặp 200; không tạo bản ghi trùng | Chưa chạy |
| RP-06 | Bỏ yêu thích | DELETE `/api/favorites/{propertyId}` | 204; gọi lặp vẫn an toàn | Chưa chạy |
| RP-07 | Kiểm tra yêu thích | GET `/api/favorites/{propertyId}/status` | Giữ nguyên trạng thái đã lưu kể cả tin tạm ẩn | Chưa chạy |
| RP-08 | Danh sách kiểm duyệt | GET `/api/admin/properties?status=Pending&page=1&pageSize=10` bằng Admin | Filter, thứ tự, metadata phân trang giữ nguyên | Chưa chạy |
| RP-09 | Chi tiết kiểm duyệt | GET `/api/admin/properties/{propertyId}` bằng Admin | Có ảnh, tiện ích và lịch sử kiểm duyệt | Chưa chạy |
| RP-10 | Quyền Admin | Gọi endpoint moderation không token / bằng Tenant | 401 / 403 | Chưa chạy |
| RP-11 | Quyền sở hữu | Landlord sửa tin của người khác | 403; không thay đổi dữ liệu | Chưa chạy |
| RP-12 | Trạng thái không hợp lệ | Lặp lại thao tác duyệt hoặc ẩn/khôi phục sai trạng thái | 409; không ghi log chuyển trạng thái mới | Chưa chạy |

## C. Build và rà soát

- [ ] `dotnet restore`
- [ ] `dotnet build -c Release` trên .NET 10
- [ ] Chạy checklist A bằng test middleware.
- [ ] Chạy checklist B với SQL Server và JWT của Tenant/Landlord/Admin.
- [ ] So sánh response contract trước/sau refactor.
- [ ] Xác nhận không sửa Entity, DbContext hoặc Migration.
