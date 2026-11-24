using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký dịch vụ.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Cấu hình Entity Framework Core và SQL Server.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:DefaultConnection.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

// Cấu hình HTTP pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
