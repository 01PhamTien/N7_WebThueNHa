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

## Hồ sơ cá nhân và Swagger Bearer

- `GET /api/auth/me`: xem hồ sơ tài khoản đang đăng nhập.
- `PUT /api/auth/profile`: cập nhật họ tên, số điện thoại và URL ảnh đại diện.
- Swagger UI có nút `Authorize`: dán Access Token nhận từ `POST /api/auth/login` (chỉ dán token, không thêm chữ Bearer).

Các endpoint có `[Authorize]` yêu cầu JWT hợp lệ. Khi xây dựng API nghiệp vụ theo vai trò, dùng `[Authorize(Roles = "Admin")]`, `[Authorize(Roles = "Landlord")]` hoặc `[Authorize(Roles = "Tenant")]`. Không cấp quyền bằng dữ liệu gửi từ client; vai trò được lấy từ bảng UserRoles khi đăng nhập.

## Quan ly tai khoan danh cho Admin

Cac API trong `AdminUsersController` chi danh cho nguoi co role `Admin`:

- `GET /api/admin/users?keyword=&isActive=&page=1&pageSize=20`: danh sach tai khoan, tim kiem va phan trang.
- `GET /api/admin/users/{userId}`: xem chi tiet tai khoan (khong tra PasswordHash).
- `PATCH /api/admin/users/{userId}/status`: khoa/mo khoa tai khoan.
- `PUT /api/admin/users/{userId}/roles`: cap nhat vai tro.

Quy tac bao ve:
- Khong cho tu khoa tai khoan dang dang nhap.
- Khong cho khoa hoac go role Admin cua quan tri vien cuoi cung dang hoat dong. Viec khoa tai khoan/cap nhat role duoc bao ve bang transaction Serializable de tranh hai request dong thoi cung xoa Admin cuoi cung.
- Khong cho quan tri vien tu thay doi role cua chinh minh.
- Role chi duoc chon tu bang Roles; client khong the tu cap role.
- Khi tai khoan bi khoa, JWT cu bi tu choi o cac request tiep theo. Khi doi role, tai khoan can dang nhap lai de nhan role claim moi.

### Tao Admin dau tien
Dang ky mot tai khoan qua API register, sau do mo `Scripts/TaoAdminDauTien.sql` trong SSMS, thay email mau bang email vua dang ky va chay script. Khong co API cong khai nao cap quyen Admin.

- JWT cu bi tu choi neu tai khoan bi khoa hoac tap role trong token khong con khop voi UserRoles trong database. Khi cap/thu hoi role, token cu khong the tiep tuc dung quyen cu; nguoi dung can dang nhap lai.

Request mẫu kiểm thử API quản trị tài khoản nằm tại `Requests/admin-users.http`, gồm tìm kiếm, phân trang, xem chi tiết, khóa/mở khóa và cập nhật vai trò.
