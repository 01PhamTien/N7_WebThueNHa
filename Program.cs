using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Web2_ThueNha.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Web2_ThueNha API",
        Version = "v1",
        Description = "API cho hệ thống quản lý cho thuê nhà."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập Access Token nhận được từ API đăng nhập. Không cần thêm tiền tố Bearer."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:DefaultConnection.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Chưa cấu hình Jwt:Key bằng User Secrets.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Web2_ThueNha";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Web2_ThueNhaClient";

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key phải có ít nhất 32 byte.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Kiểm tra trạng thái tài khoản ở mỗi request để token cũ không dùng được sau khi bị khóa.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdValue, out var userId))
                {
                    context.Fail("Token không chứa mã người dùng hợp lệ.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var user = await db.Users
                    .AsNoTracking()
                    .Include(x => x.UserRoles)
                        .ThenInclude(x => x.Role)
                    .SingleOrDefaultAsync(x => x.Id == userId);

                if (user is null || !user.IsActive)
                {
                    context.Fail("Tài khoản không tồn tại hoặc đã bị khóa.");
                    return;
                }

                // Vai trò trong JWT phải còn khớp với database để thu hồi quyền có hiệu lực ngay.
                var tokenRoles = context.Principal?
                    .FindAll(System.Security.Claims.ClaimTypes.Role)
                    .Select(x => x.Value)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var currentRoles = user.UserRoles.Select(x => x.Role.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!tokenRoles.SetEquals(currentRoles))
                    context.Fail("Vai trò tài khoản đã thay đổi. Vui lòng đăng nhập lại.");
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<User>, Microsoft.AspNetCore.Identity.PasswordHasher<User>>();

var app = builder.Build();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
