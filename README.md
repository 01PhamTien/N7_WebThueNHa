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
2. Cài SQL Server LocalDB hoặc SQL Server phù hợp.
3. Mở solution `Web2_ThueNha.slnx` bằng Visual Studio.
4. Chạy `dotnet restore`.
5. Chạy `dotnet build`.
6. Chạy `dotnet run`.

## Cấu hình Database
Chuỗi kết nối mặc định trong `appsettings.json` dùng SQL Server LocalDB:
`(localdb)\\MSSQLLocalDB`.

Nếu dùng SQL Server instance khác, thay `ConnectionStrings:DefaultConnection` trong cấu hình local hoặc dùng .NET User Secrets. Không commit mật khẩu, API key hoặc secret lên Git.

## EF Core CLI
Cài công cụ EF Core đúng phiên bản:
```bash
dotnet tool install --global dotnet-ef --version 10.0.15
```

Sau khi các Entity và cấu hình quan hệ hoàn tất, tạo migration:
```bash
dotnet ef migrations add TaoCSDL_BanDau
dotnet ef database update
```

## Quy trình Git
- `main`: phiên bản ổn định.
- `develop`: nhánh tích hợp.
- `feature/*`: nhánh làm từng chức năng.
- Tạo Pull Request từ feature vào develop; nhóm trưởng review và merge.
