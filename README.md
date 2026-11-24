# Web2_ThueNha

Backend hệ thống cho thuê nhà, xây dựng bằng ASP.NET Core Web API trên .NET 10.

## Công nghệ
- .NET 10
- ASP.NET Core Web API
- Entity Framework Core 10
- SQL Server
- Code First / EF Core Migrations

## Chạy dự án
1. Cài .NET 10 SDK.
2. Cài SQL Server Database Engine (nhóm đang dùng SQL Server Express instance).
3. Cài SQL Server Management Studio (SSMS) để quản lý dữ liệu.
4. Mở solution bằng Visual Studio.
5. Chạy `dotnet restore`.
6. Cấu hình User Secrets theo hướng dẫn bên dưới.
7. Chạy `dotnet build` và `dotnet run`.

## Cấu hình Database cho máy phát triển

Không lưu connection string riêng của từng máy trong Git. Project đã khai báo User Secrets ID: `Web2_ThueNha-Development`.

Ví dụ cho SQL Server Express với Windows Authentication:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=TEN_MAY\\SQLEXPRESS;Database=Web2_ThueNhaDb;Trusted_Connection=True;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True"
```

Thay `TEN_MAY` bằng tên máy SQL Server thực tế. Không đưa mật khẩu, token hoặc secret lên Git.

## EF Core CLI

Cài công cụ EF Core đúng phiên bản:

```bash
dotnet tool install --global dotnet-ef --version 10.0.15
```

Sau khi Entity và cấu hình quan hệ hoàn tất, tạo migration:

```bash
dotnet ef migrations add TaoCSDL_BanDau
dotnet ef database update
```

## Quy trình Git
- `main`: phiên bản ổn định.
- `develop`: nhánh tích hợp.
- `feature/*`: nhánh làm từng chức năng.
- Tạo Pull Request từ feature vào develop; nhóm trưởng review và merge.


## Xác thực tài khoản và JWT

Các API xác thực ban đầu:

- `POST /api/auth/register`: đăng ký tài khoản; tài khoản mới luôn nhận vai trò `Tenant`.
- `POST /api/auth/login`: đăng nhập và nhận JWT Access Token (hiệu lực 2 giờ).
- `GET /api/auth/me`: lấy thông tin tài khoản hiện tại; yêu cầu Bearer Token hợp lệ.

Mật khẩu được băm bằng ASP.NET Core Identity PasswordHasher; không lưu mật khẩu dạng rõ. Không cho phép người dùng tự chọn vai trò Admin hoặc Landlord khi đăng ký công khai.

### Cấu hình JWT bằng User Secrets (PowerShell)

Tạo khóa bí mật ngẫu nhiên tối thiểu 32 byte và lưu trực tiếp vào User Secrets:

```powershell
$jwtKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set "Jwt:Key" $jwtKey
```

Có thể kiểm tra các khóa đã lưu bằng:

```powershell
dotnet user-secrets list
```

Không chia sẻ hoặc commit giá trị `Jwt:Key` lên Git. Mỗi máy phát triển có thể tự tạo khóa riêng. Các giá trị mặc định của Issuer và Audience lần lượt là `Web2_ThueNha` và `Web2_ThueNhaClient`.

### Thử API

1. Chạy ứng dụng trong môi trường Development.
2. Mở Swagger UI.
3. Gọi `POST /api/auth/register` với FullName, Email, Password (ít nhất 8 ký tự) và PhoneNumber tùy chọn.
4. Gọi `POST /api/auth/login` bằng Email và Password để nhận `accessToken`.
5. Gửi token dưới dạng `Authorization: Bearer <accessToken>` khi gọi `GET /api/auth/me`.

Không dùng tài khoản hoặc dữ liệu thật của người dùng trong môi trường thử nghiệm.

## API Admin kiểm duyệt tin đăng

Các endpoint kiểm duyệt yêu cầu JWT có role `Admin`. Không nhận `AdminId` từ client; mã Admin được lấy từ claim `ClaimTypes.NameIdentifier`.

| Phương thức | Endpoint | Chức năng |
|---|---|---|
| GET | `/api/admin/properties?status=Pending&page=1&pageSize=10` | Danh sách tin theo trạng thái kiểm duyệt, mặc định Pending, có phân trang server-side |
| GET | `/api/admin/properties/{propertyId}` | Chi tiết tin, hình ảnh, tiện ích và lịch sử kiểm duyệt |
| POST | `/api/admin/properties/{propertyId}/approve` | Duyệt tin đang Pending |
| POST | `/api/admin/properties/{propertyId}/reject` | Từ chối tin Pending, body bắt buộc có `reason` |
| POST | `/api/admin/properties/{propertyId}/hide` | Ẩn tin Approved + Published, body bắt buộc có `reason` |
| POST | `/api/admin/properties/{propertyId}/restore` | Khôi phục tin Approved + Hidden về Published, body bắt buộc có `reason` |

Quy tắc chuyển trạng thái:

- Chỉ tin `Pending` mới được duyệt hoặc từ chối. Từ chối chuyển `ListingStatus` về `Draft`.
- Chỉ tin `Approved + Published` mới được ẩn; chỉ tin `Approved + Hidden` mới được khôi phục.
- Lý do từ chối/ẩn/khôi phục phải dài từ 5 đến 1000 ký tự.
- Mỗi chuyển trạng thái hợp lệ tạo bản ghi `PropertyModerationLogs` với AdminId, PropertyId, Action, Reason và CreatedAt.
- API danh sách lọc trước `CountAsync`, sau đó `OrderBy`, `Skip`, `Take` trên IQueryable; `pageSize` tối đa 50.

Request mẫu và checklist nằm tại `docs/requests/property-moderation.http.md` và `docs/requests/property-moderation-test-cases.md`.
