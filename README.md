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
