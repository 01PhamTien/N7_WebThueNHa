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

## Phân trang API danh sách nhà của chủ nhà

Endpoint `GET /api/properties/mine` yêu cầu JWT có role `Landlord` và hỗ trợ query parameters:

- `page`: số trang, mặc định `1`, phải lớn hơn hoặc bằng 1.
- `pageSize`: số bản ghi mỗi trang, mặc định `10`, giới hạn từ 1 đến 50.
- `status`: tùy chọn, lọc theo trạng thái tin đăng (`Draft`, `Published`, `Hidden`, `Closed`).

Ví dụ:

```http
GET /api/properties/mine?page=1&pageSize=10&status=Draft
Authorization: Bearer <landlordAccessToken>
```

API lấy chủ nhà từ JWT Claims, không nhận `LandlordId` từ client. Bộ lọc được áp dụng trước `CountAsync`; dữ liệu được sắp xếp ổn định theo `CreatedAt` giảm dần và `Id` giảm dần, sau đó phân trang bằng `Skip/Take` trên truy vấn EF Core. Response gồm `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasPrevious` và `hasNext`. Tham số không hợp lệ trả về `400`; trang vượt quá tổng số trang trả về danh sách rỗng và metadata tương ứng.

Request mẫu và checklist chi tiết nằm tại `docs/requests/property-management.http.md` và `docs/requests/property-management-test-cases.md`.

### Quy tắc kiểm duyệt hình ảnh

Khi chủ nhà thêm ảnh, xóa ảnh hoặc đổi ảnh chính, tin đăng được chuyển về `ModerationStatus=Pending` và `ListingStatus=Draft`. Thay đổi hình ảnh là thay đổi nội dung tin, vì vậy phải được Admin duyệt lại trước khi xuất hiện công khai. API danh sách nhà công khai trả metadata phân trang đầy đủ và dùng thứ tự ổn định theo `CreatedAt`/ `Id`.
