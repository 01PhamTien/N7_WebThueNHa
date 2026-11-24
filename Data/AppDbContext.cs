using Microsoft.EntityFrameworkCore;

namespace Web2_ThueNha.Data;

/// <summary>
/// DbContext trung tâm của hệ thống cho thuê nhà.
/// Các DbSet và cấu hình quan hệ sẽ được bổ sung cùng các Entity.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Cấu hình Fluent API cho 21 Entity sẽ được bổ sung tại đây
        // hoặc tách thành các lớp IEntityTypeConfiguration riêng.
    }
}
