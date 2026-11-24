using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký dịch vụ API và Swagger.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Web2_ThueNha API",
        Version = "v1",
        Description = "API cho hệ thống quản lý cho thuê nhà."
    });
});

// Cấu hình Entity Framework Core và SQL Server.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:DefaultConnection.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

// Chỉ bật Swagger UI trong môi trường phát triển.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Web2_ThueNha API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
