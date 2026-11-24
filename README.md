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
- Khong cho khoa hoac go role Admin cua quan tri vien cuoi cung dang hoat dong.
- Khong cho quan tri vien tu thay doi role cua chinh minh.
- Role chi duoc chon tu bang Roles; client khong the tu cap role.
- Khi tai khoan bi khoa, JWT cu bi tu choi o cac request tiep theo. Khi doi role, tai khoan can dang nhap lai de nhan role claim moi.

### Tao Admin dau tien
Dang ky mot tai khoan qua API register, sau do mo `Scripts/TaoAdminDauTien.sql` trong SSMS, thay email mau bang email vua dang ky va chay script. Khong co API cong khai nao cap quyen Admin.

- JWT cu bi tu choi neu tai khoan bi khoa hoac tap role trong token khong con khop voi UserRoles trong database. Khi cap/thu hoi role, token cu khong the tiep tuc dung quyen cu; nguoi dung can dang nhap lai.

## Chat rieng theo tung can nha

Tat ca API chat deu yeu cau JWT:

- `POST /api/chat/properties/{propertyId}/conversation`: nguoi co role Tenant tao/lay hoi thoai voi chu nha cua can nha da duoc duyet va dang cong khai.
- `GET /api/chat/conversations?page=1&pageSize=20&includeArchived=false`: danh sach hoi thoai phan trang; moi item co `unreadCount` tinh tu `LastReadAt` cua participant.
- `GET /api/chat/conversations/{conversationId}/messages?page=1&pageSize=30`: lich su tin nhan co phan trang.
- `POST /api/chat/conversations/{conversationId}/messages`: gui tin nhan van ban.
- `POST /api/chat/conversations/{conversationId}/attachments`: gui tep multipart/form-data (toi da 10 MB; JPG/JPEG/PNG/WEBP/PDF/DOC/DOCX).
- `GET /api/chat/attachments/{attachmentId}`: tai tep; chi thanh vien hoi thoai duoc tai.
- `PATCH /api/chat/conversations/{conversationId}/read`: danh dau da doc.
- `PATCH /api/chat/conversations/{conversationId}/archive`: an/hien hoi thoai trong danh sach ca nhan.

Quy tac: moi Property + Tenant + Landlord chi co mot hoi thoai; unique index trong database chong tao trung. Chi participant moi doc/gửi tin va tai tep. Tep duoc luu ngoai wwwroot trong `PrivateUploads/chat`, khong phuc vu cong khai; thu muc nay duoc gitignore. Moi may dev can giu backup rieng neu muon giu tep tai len. Realtime duoc trien khai bang SignalR tai `/hubs/chat`; client ket noi bang JWT, goi `JoinConversation(conversationId)` sau khi vao man hinh hoi thoai va lang nghe su kien `NhanTinNhan`. Server chi cho participant tham gia group.

### SignalR realtime

- Hub endpoint: `/hubs/chat`.
- Client ket noi bang JWT, sau do goi `JoinConversation(conversationId)` de vao group.
- Server chi cho phep user la participant vao group cua hoi thoai.
- Khi co tin nhan moi (van ban/tep), server phat event `NhanTinNhan` toi group.
- Goi `LeaveConversation(conversationId)` khi roi man hinh chat.

Voi SignalR JavaScript client, cung cap `accessTokenFactory: () => token`. Backend chi doc query `access_token` tren duong dan `/hubs/chat`; khong doc token query tren cac API khac. Nho dung HTTPS trong moi truong that.

- Upload tep chat kiem tra ca extension, MIME va magic bytes (chu ky noi dung JPG/PNG/WEBP/PDF/DOC/DOCX), khong chi tin ten file va Content-Type do client gui.

- `PUT /api/chat/conversations/{conversationId}/messages/{messageId}`: sua tin nhan van ban cua chinh nguoi gui.
- `DELETE /api/chat/conversations/{conversationId}/messages/{messageId}`: xoa mem tin nhan cua chinh nguoi gui; phat event SignalR `XoaTinNhan`.
- Khi sua tin nhan, server phat event SignalR `SuaTinNhan`. Tin nhan da xoa khong hien trong lich su va tep cua tin da xoa khong con tai duoc qua API.

Request mau cho cac API chat nam trong `Requests/chat.http`; SignalR event va cach ket noi duoc mo ta ngay trong file va phan SignalR ben tren.

- `GET /api/chat/conversations?includeArchived=true`: xem cả hội thoại đã ẩn để người dùng có thể hiện lại; mặc định API chỉ trả hội thoại chưa ẩn.

Request mẫu kiểm thử API quản trị tài khoản nằm tại `Requests/admin-users.http`, gồm tìm kiếm, phân trang, xem chi tiết, khóa/mở khóa và cập nhật vai trò.
Danh sach hoi thoai va lich su tin nhan dung phan trang server-side; page/pageSize sai tra 400, trang vuot `totalPages` tra items rong. Thay doi hinh anh/tep trong chat chi tai duoc qua API co JWT va kiem tra participant; ten file duoc lam sach truoc khi luu.
