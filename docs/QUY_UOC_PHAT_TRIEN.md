# Quy ước phát triển Web2_ThueNha theo nội dung đã học

Tài liệu này thống nhất cách triển khai để code của nhóm bám sát các nội dung ASP.NET Core Web API đã học trên lớp. Ưu tiên code rõ ràng, dễ giải thích và đúng kiến thức môn học; không thêm framework/pattern phức tạp nếu chưa cần.

## 1. Công nghệ và phạm vi

- ASP.NET Core Web API trên .NET 10.
- Controller, Attribute Routing, Model Binding, HTTP Methods và RESTful API.
- DTO riêng cho Request/Response; không nhận/trả trực tiếp Entity trong API.
- Data Annotations và ModelState để validate dữ liệu đầu vào.
- Swagger/OpenAPI để xem và thử API.
- Entity Framework Core Code First, Migration, SQL Server.
- LINQ và quan hệ dữ liệu qua navigation properties.
- Dependency Injection cho DbContext và Service.
- JWT Authentication; Authorization bằng Role, Claims và Policy khi cần.
- Middleware dùng cho pipeline chung như exception handling/logging; không nhét nghiệp vụ vào Middleware.

## 2. Cấu trúc nhiều tầng vừa đủ

Dùng cấu trúc đơn giản, nhất quán:

- `Controllers/`: nhận HTTP request, kiểm tra ModelState/quyền, gọi service và trả HTTP status.
- `DTOs/<Feature>/`: request/response DTO và query DTO.
- `Services/`: xử lý nghiệp vụ, kiểm tra quyền sở hữu và điều phối thao tác dữ liệu khi nghiệp vụ đủ phức tạp.
- `Data/`: AppDbContext và cấu hình EF Core.
- `Models/`: Entity và quan hệ dữ liệu.
- `Middleware/`: xử lý lỗi/logging chung nếu cần.
- `Migrations/`: migration do EF Core sinh.

Không bắt buộc tạo Repository Pattern cho mọi Entity. Dùng trực tiếp AppDbContext trong service là phù hợp với EF Core và phạm vi môn học; chỉ tách Repository nếu có lý do cụ thể và được nhóm thống nhất.

## 3. Controller, Routing, HTTP và Action Result

- Dùng `[ApiController]`, `[Route("api/[controller]")]` và attribute routing.
- Dùng đúng HTTP method: GET đọc, POST tạo/thực hiện lệnh tạo, PUT cập nhật toàn bộ, PATCH cập nhật một phần, DELETE xóa.
- Ưu tiên `ActionResult<T>` cho API có response model.
- Trả status code phù hợp: 200 OK, 201 Created, 204 NoContent, 400 BadRequest, 401 Unauthorized, 403 Forbid, 404 NotFound, 409 Conflict.
- Controller không chứa truy vấn LINQ dài hoặc toàn bộ nghiệp vụ; chuyển phần xử lý phù hợp vào Service.
- Không tin UserId, Role, LandlordId hay giá trị quyền do client gửi lên. Lấy danh tính từ JWT Claims và kiểm tra quyền trên server.

## 4. DTO và Validation

- Mỗi chức năng có DTO riêng, ví dụ `CreatePropertyRequest`, `UpdatePropertyRequest`, `PropertyResponse`.
- Không bind trực tiếp Entity từ body để tránh over-posting.
- Dùng Data Annotations như `[Required]`, `[StringLength]`, `[Range]`, `[EmailAddress]` và kiểm tra quy tắc nghiệp vụ trong Service.
- Với `[ApiController]`, lỗi validation cơ bản tự trả 400; không cần lặp kiểm tra ModelState ở mọi action nếu không có xử lý đặc biệt.
- Không trả PasswordHash, thông tin nội bộ hoặc navigation graph không cần thiết.

## 5. Phân trang phía Server bằng Query Parameters

Đây là quy ước quan trọng của dự án: phân trang được thực hiện ở **Backend/SQL Server**, không phải tải toàn bộ danh sách về Frontend rồi mới cắt mảng.

Ví dụ API:

`GET /api/properties?page=1&pageSize=10&keyword=studio&province=Hue`

Query parameters:
- `page`: trang cần lấy, bắt đầu từ 1.
- `pageSize`: số bản ghi mỗi trang; đặt giới hạn tối đa, ví dụ 100.
- Các filter như `keyword`, `province`, `status` là tùy endpoint.

Quy trình LINQ chuẩn:
1. Bắt đầu từ `IQueryable` của DbSet.
2. Áp dụng filter/search trước.
3. Áp dụng `OrderBy` ổn định trước khi phân trang.
4. Dùng `CountAsync()` để lấy tổng số bản ghi sau filter.
5. Dùng `Skip((page - 1) * pageSize).Take(pageSize)`.
6. Dùng `Select` sang DTO và `ToListAsync()`.

Ví dụ minh họa:

```csharp
var query = _context.Properties
    .AsNoTracking()
    .Where(x => x.ListingStatus == PropertyListingStatus.Published
             && x.ModerationStatus == PropertyModerationStatus.Approved);

if (!string.IsNullOrWhiteSpace(keyword))
{
    query = query.Where(x => x.Title.Contains(keyword));
}

var totalCount = await query.CountAsync(cancellationToken);

var items = await query
    .OrderByDescending(x => x.CreatedAt)
    .ThenBy(x => x.Id)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .Select(x => new PropertyListResponse
    {
        Id = x.Id,
        Title = x.Title,
        PricePerMonth = x.PricePerMonth
    })
    .ToListAsync(cancellationToken);
```

Response nên có cả dữ liệu và metadata phân trang:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 10,
  "totalCount": 45,
  "totalPages": 5,
  "hasPrevious": false,
  "hasNext": true
}
```

Frontend chỉ gửi query parameters và hiển thị dữ liệu/metadata backend trả về. Không tải tất cả bản ghi rồi tự phân trang ở client. Không gọi `ToList()`/`AsEnumerable()` trước filter, Count, Skip và Take vì sẽ làm mất lợi ích phân trang tại SQL Server.

## 6. EF Core, LINQ, Code First và Migration

- Dùng `AppDbContext` đã đăng ký bằng DI.
- Dùng async query: `ToListAsync`, `SingleOrDefaultAsync`, `AnyAsync`, `CountAsync`, `SaveChangesAsync`.
- Dùng `AsNoTracking()` cho truy vấn chỉ đọc.
- Kiểm tra quan hệ và DeleteBehavior trong AppDbContext trước khi thêm truy vấn có Include.
- Không sửa migration đã được chia sẻ/đã áp dụng trên máy thành viên; tạo migration mới khi schema thay đổi và báo nhóm trưởng review.
- Không tự ý đổi Entity/DbContext/Migration thuộc phần người khác đang phụ trách.
- Connection string và JWT key phải nằm trong User Secrets/Environment Variables, không commit lên Git.

Lệnh EF Core CLI thường dùng:

```powershell
dotnet ef migrations add TenMigration
dotnet ef database update
dotnet ef migrations list
```

Các package/công cụ liên quan:
- `Microsoft.EntityFrameworkCore`: API lõi EF Core; được provider SQL Server tham chiếu phụ thuộc.
- `Microsoft.EntityFrameworkCore.SqlServer`: provider kết nối SQL Server.
- `Microsoft.EntityFrameworkCore.Design`: hỗ trợ công cụ design-time và `dotnet ef`; dự án hiện đã tham chiếu package này.
- `Microsoft.EntityFrameworkCore.Tools`: dùng khi chạy lệnh EF Core trong Package Manager Console của Visual Studio; chỉ thêm nếu nhóm dùng workflow PMC.
- `dotnet-ef`: công cụ CLI cài riêng bằng `dotnet tool install --global dotnet-ef --version 10.0.15`.

Không cài package/công cụ khác phiên bản major với EF Core của dự án. Hiện project đang dùng EF Core 10.0.15, SQL Server provider 10.0.15 và Design 10.0.15.

## 7. Dependency Injection và Service

- Đăng ký `AppDbContext` bằng `AddDbContext` (Scoped mặc định).
- Service làm việc với DbContext thông thường đăng ký `AddScoped<IService, Service>()`.
- Controller nhận service qua constructor injection.
- Không tự `new AppDbContext`, không dùng Service Locator trong nghiệp vụ thông thường.
- Không đăng ký DbContext/Service giữ trạng thái request thành Singleton.

## 8. JWT, Authentication và Authorization

- Authentication xác minh token; Authorization quyết định user có được phép thực hiện hành động hay không.
- Dùng `[Authorize]` để yêu cầu đăng nhập.
- Dùng `[Authorize(Roles = "Admin")]` hoặc Role phù hợp cho chức năng theo vai trò.
- Dùng Claims để lấy UserId hiện tại; không nhận UserId từ client làm căn cứ phân quyền.
- Dùng Policy khi quy tắc quyền phức tạp hoặc cần tái sử dụng.
- Role không thay thế kiểm tra quyền sở hữu tài nguyên: Landlord chỉ được sửa căn nhà của mình; Tenant chỉ xem booking/hợp đồng của mình.
- Phân biệt 401 (chưa xác thực/token không hợp lệ) và 403 (đã đăng nhập nhưng không đủ quyền).

## 9. Swagger và kiểm thử

- API mới phải có thể thử được qua Swagger; mô tả DTO và response rõ ràng.
- API cần JWT phải cấu hình Bearer trong Swagger và ghi rõ endpoint yêu cầu quyền.
- Mỗi feature cần request mẫu hoặc test case: dữ liệu hợp lệ, dữ liệu sai, chưa đăng nhập, sai role, không sở hữu tài nguyên, dữ liệu không tồn tại và trường hợp biên.
- Build CI xanh là điều kiện cần, không thay thế kiểm thử API với SQL Server.
- Ghi lại request, status code và kết quả thực tế trong PR; không đánh dấu test thành công nếu chưa chạy.

## 10. Quy ước Git và review

- `main`: bản ổn định; `develop`: tích hợp; `feature/*`: nhánh chức năng.
- Mỗi feature PR vào `develop`; nhóm trưởng review và merge.
- Commit tiếng Việt, mô tả đúng thay đổi; chia commit theo phần chức năng/test/docs có ý nghĩa, không tạo commit vụn chỉ để tăng số lượng.
- Không sửa file thuộc feature khác nếu chưa trao đổi.
- PR cần có: mục tiêu, phạm vi, API thay đổi, cách chạy/test, checklist đã thực sự kiểm tra và ảnh/log nếu cần.

---
Tài liệu này là quy ước triển khai cho đồ án Web2_ThueNha, ưu tiên bám sát nội dung ASP.NET Core Web API và EF Core đã học.
