using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<PaymentDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Payments")
        ?? Environment.GetEnvironmentVariable("PAYMENTS_DB_CONNECTION");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Thiếu cấu hình ConnectionStrings:Payments hoặc PAYMENTS_DB_CONNECTION.");
    }

    options.UseSqlServer(connectionString);
});

builder.Services.AddScoped<ISePayService, SePayService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
