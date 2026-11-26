using Microsoft.EntityFrameworkCore;

namespace Web2_ThueNha.Data;

/// <summary>DbContext trung tâm của hệ thống Web2_ThueNha.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyType> PropertyTypes => Set<PropertyType>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<PropertyAmenity> PropertyAmenities => Set<PropertyAmenity>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<RentalContract> RentalContracts => Set<RentalContract>();
    public DbSet<MoveOutRequest> MoveOutRequests => Set<MoveOutRequest>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PropertyModerationLog> PropertyModerationLogs => Set<PropertyModerationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20);
            e.Property(x => x.AvatarUrl).HasMaxLength(1000);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(
                new Role { Id = 1, Name = "Admin", Description = "Quản trị hệ thống" },
                new Role { Id = 2, Name = "Landlord", Description = "Chủ nhà cho thuê" },
                new Role { Id = 3, Name = "Tenant", Description = "Người thuê nhà" }
            );
        });

        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("UserRoles");
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Property>(e =>
        {
            e.ToTable("Properties");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.PricePerMonth).HasPrecision(18, 2);
            e.Property(x => x.DepositAmount).HasPrecision(18, 2);
            e.Property(x => x.FurnishingDescription).HasMaxLength(1000);
            e.Property(x => x.AdditionalFeesDescription).HasMaxLength(1000);
            e.Property(x => x.Area).HasPrecision(10, 2);
            e.Property(x => x.AddressLine).HasMaxLength(300).IsRequired();
            e.Property(x => x.WardName).HasMaxLength(120);
            e.Property(x => x.ProvinceName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Latitude).HasPrecision(10, 7);
            e.Property(x => x.Longitude).HasPrecision(10, 7);
            e.HasOne(x => x.Landlord).WithMany(x => x.Properties).HasForeignKey(x => x.LandlordId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PropertyType).WithMany(x => x.Properties).HasForeignKey(x => x.PropertyTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ListingStatus, x.ModerationStatus, x.ProvinceName });
        });

        modelBuilder.Entity<PropertyType>(e =>
        {
            e.ToTable("PropertyTypes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(
                new PropertyType { Id = 1, Name = "Phòng trọ", Description = "Phòng cho thuê trong nhà trọ hoặc nhà ở" },
                new PropertyType { Id = 2, Name = "Căn hộ", Description = "Căn hộ chung cư độc lập" },
                new PropertyType { Id = 3, Name = "Nhà nguyên căn", Description = "Nhà cho thuê nguyên căn" },
                new PropertyType { Id = 4, Name = "Studio", Description = "Căn hộ studio" },
                new PropertyType { Id = 5, Name = "Mặt bằng", Description = "Mặt bằng cho thuê" }
            );
        });

        modelBuilder.Entity<PropertyImage>(e =>
        {
            e.ToTable("PropertyImages");
            e.HasKey(x => x.Id);
            e.Property(x => x.ImageUrl).HasMaxLength(1000).IsRequired();
            e.HasOne(x => x.Property).WithMany(x => x.Images).HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.PropertyId, x.DisplayOrder });
        });

        modelBuilder.Entity<Amenity>(e =>
        {
            e.ToTable("Amenities");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(
                new Amenity { Id = 1, Name = "Wi-Fi", Description = "Có kết nối Internet" },
                new Amenity { Id = 2, Name = "Điều hòa", Description = "Có thiết bị điều hòa không khí" },
                new Amenity { Id = 3, Name = "Máy giặt", Description = "Có máy giặt sử dụng chung hoặc riêng" },
                new Amenity { Id = 4, Name = "Chỗ để xe", Description = "Có khu vực để xe" },
                new Amenity { Id = 5, Name = "Thang máy", Description = "Có thang máy" },
                new Amenity { Id = 6, Name = "Bảo vệ", Description = "Có dịch vụ bảo vệ" },
                new Amenity { Id = 7, Name = "Ban công", Description = "Có ban công" },
                new Amenity { Id = 8, Name = "Nội thất", Description = "Có trang bị nội thất" }
            );
        });

        modelBuilder.Entity<PropertyAmenity>(e =>
        {
            e.ToTable("PropertyAmenities");
            e.HasKey(x => new { x.PropertyId, x.AmenityId });
            e.HasOne(x => x.Property).WithMany(x => x.PropertyAmenities).HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Amenity).WithMany(x => x.PropertyAmenities).HasForeignKey(x => x.AmenityId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.ToTable("Bookings");
            e.HasKey(x => x.Id);
            e.Property(x => x.ProposedPrice).HasPrecision(18, 2);
            e.Property(x => x.Message).HasMaxLength(1000);
            e.Property(x => x.RejectionReason).HasMaxLength(1000);
            e.HasOne(x => x.Property).WithMany(x => x.Bookings).HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Tenant).WithMany(x => x.Bookings).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PropertyId, x.StartDate, x.EndDate, x.Status });
        });

        modelBuilder.Entity<RentalContract>(e =>
        {
            e.ToTable("RentalContracts");
            e.HasKey(x => x.Id);
            e.Property(x => x.ContractCode).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.ContractCode).IsUnique();
            e.Property(x => x.MonthlyRent).HasPrecision(18, 2);
            e.Property(x => x.DepositAmount).HasPrecision(18, 2);
            e.Property(x => x.Terms).HasMaxLength(4000);
            e.Property(x => x.ContractUrl).HasMaxLength(1000);
            e.HasOne(x => x.Booking).WithOne(x => x.RentalContract).HasForeignKey<RentalContract>(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Landlord).WithMany().HasForeignKey(x => x.LandlordId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invoice>(e =>
        {
            e.ToTable("Invoices");
            e.HasKey(x => x.Id);
            e.Property(x => x.InvoiceCode).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.InvoiceCode).IsUnique();
            e.HasIndex(x => new { x.ContractId, x.InvoiceType })
                .IsUnique()
                .HasDatabaseName("UX_Invoices_Contract_Deposit")
                .HasFilter("[InvoiceType] = 0 AND [Status] <> 5");
            e.HasIndex(x => new { x.ContractId, x.InvoiceType, x.PeriodStart, x.PeriodEnd })
                .IsUnique()
                .HasDatabaseName("UX_Invoices_Contract_MonthlyRent_Period")
                .HasFilter("[InvoiceType] = 1 AND [PeriodStart] IS NOT NULL AND [PeriodEnd] IS NOT NULL AND [Status] <> 5");
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.HasOne(x => x.Contract).WithMany(x => x.Invoices).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceItem>(e =>
        {
            e.ToTable("InvoiceItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.Description).HasMaxLength(300).IsRequired();
            e.Property(x => x.Quantity).HasPrecision(18, 2);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasOne(x => x.Invoice).WithMany(x => x.Items).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("Payments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.PaymentProvider).HasMaxLength(100);
            e.Property(x => x.TransactionCode).HasMaxLength(150);
            e.Property(x => x.FailureReason).HasMaxLength(1000);
            e.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Payer).WithMany().HasForeignKey(x => x.PayerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ReviewedById);
            e.HasIndex(x => x.TransactionCode).IsUnique().HasFilter("[TransactionCode] IS NOT NULL");
        });

        modelBuilder.Entity<Conversation>(e =>
        {
            e.ToTable("Conversations");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Landlord).WithMany().HasForeignKey(x => x.LandlordId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Booking).WithMany(x => x.Conversations).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.PropertyId, x.TenantId, x.LandlordId }).IsUnique();
        });

        modelBuilder.Entity<ConversationParticipant>(e =>
        {
            e.ToTable("ConversationParticipants");
            e.HasKey(x => new { x.ConversationId, x.UserId });
            e.HasOne(x => x.Conversation).WithMany(x => x.Participants).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Message>(e =>
        {
            e.ToTable("Messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).HasMaxLength(4000);
            e.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ConversationId, x.SentAt });
        });

        modelBuilder.Entity<MessageAttachment>(e =>
        {
            e.ToTable("MessageAttachments");
            e.HasKey(x => x.Id);
            e.Property(x => x.FileUrl).HasMaxLength(1000).IsRequired();
            e.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(150);
            e.HasOne(x => x.Message).WithMany(x => x.Attachments).HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Favorite>(e =>
        {
            e.ToTable("Favorites");
            e.HasKey(x => new { x.UserId, x.PropertyId });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MoveOutRequest>(e =>
        {
            e.ToTable("MoveOutRequests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            e.Property(x => x.TenantNotes).HasMaxLength(2000);
            e.Property(x => x.LandlordNotes).HasMaxLength(2000);
            e.Property(x => x.ElectricReading).HasPrecision(18, 2);
            e.Property(x => x.WaterReading).HasPrecision(18, 2);
            e.Property(x => x.DepositPaid).HasPrecision(18, 2);
            e.Property(x => x.Outstanding).HasPrecision(18, 2);
            e.Property(x => x.ExtraCharges).HasPrecision(18, 2);
            e.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Landlord).WithMany().HasForeignKey(x => x.LandlordId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.HasIndex(x => new { x.LandlordId, x.CreatedAt });
        });

        modelBuilder.Entity<Review>(e =>
        {
            e.ToTable("Reviews", table =>
                table.HasCheckConstraint("CK_Reviews_Rating", "[Rating] >= 1 AND [Rating] <= 5"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.HasOne(x => x.Contract).WithOne(x => x.Review).HasForeignKey<Review>(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("Notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            e.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
        });

        modelBuilder.Entity<PropertyModerationLog>(e =>
        {
            e.ToTable("PropertyModerationLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Admin).WithMany().HasForeignKey(x => x.AdminId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PropertyId, x.CreatedAt });
        });
    }
}
