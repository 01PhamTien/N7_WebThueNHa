using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Models;

namespace Web2_ThueNha.Data;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<SePayWebhookEvent> WebhookEvents => Set<SePayWebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var payment = modelBuilder.Entity<PaymentRecord>();
        payment.HasKey(x => x.Id);
        payment.Property(x => x.InvoiceNumber).HasMaxLength(40).IsRequired();
        payment.HasIndex(x => x.InvoiceNumber).IsUnique();
        payment.Property(x => x.Amount).HasPrecision(18, 2);
        payment.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        payment.Property(x => x.Description).HasMaxLength(255).IsRequired();
        payment.Property(x => x.Status).HasMaxLength(20).IsRequired();
        payment.Property(x => x.ProviderTransactionId).HasMaxLength(100);
        payment.HasIndex(x => x.ProviderTransactionId)
            .IsUnique()
            .HasFilter("[ProviderTransactionId] IS NOT NULL");

        var webhook = modelBuilder.Entity<SePayWebhookEvent>();
        webhook.HasKey(x => x.Id);
        webhook.Property(x => x.EventKey).HasMaxLength(120).IsRequired();
        webhook.HasIndex(x => x.EventKey).IsUnique();
        webhook.Property(x => x.RawPayload).HasColumnType("nvarchar(max)").IsRequired();
        webhook.Property(x => x.Source).HasMaxLength(30).IsRequired();
    }
}
