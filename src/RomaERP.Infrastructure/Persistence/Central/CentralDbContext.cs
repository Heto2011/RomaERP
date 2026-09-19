using Microsoft.EntityFrameworkCore;
using RomaERP.Domain.Support;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Infrastructure.Persistence.Central;

/// <summary>The one shared database: a registry mapping company codes to their own isolated tenant databases.</summary>
public class CentralDbContext : DbContext
{
    public CentralDbContext(DbContextOptions<CentralDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionInvoice> SubscriptionInvoices => Set<SubscriptionInvoice>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketMessage> SupportTicketMessages => Set<SupportTicketMessage>();
    public DbSet<SupportTicketAttachment> SupportTicketAttachments => Set<SupportTicketAttachment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(b =>
        {
            b.Property(t => t.CompanyCode).HasMaxLength(50).IsRequired();
            b.HasIndex(t => t.CompanyCode).IsUnique();
            b.Property(t => t.CompanyNameAr).HasMaxLength(200).IsRequired();
            b.Property(t => t.CompanyNameEn).HasMaxLength(200).IsRequired();
            b.Property(t => t.DatabaseName).HasMaxLength(100).IsRequired();
            b.HasQueryFilter(t => !t.IsDeleted);
        });

        builder.Entity<SubscriptionPlan>(b =>
        {
            b.Property(p => p.Code).HasMaxLength(50).IsRequired();
            b.HasIndex(p => p.Code).IsUnique();
            b.Property(p => p.NameAr).HasMaxLength(100).IsRequired();
            b.Property(p => p.NameEn).HasMaxLength(100).IsRequired();
            b.Property(p => p.MonthlyBasePrice).HasPrecision(18, 2);
            b.HasQueryFilter(p => !p.IsDeleted);
        });

        builder.Entity<Subscription>(b =>
        {
            b.HasIndex(s => s.TenantId).IsUnique();
            b.Property(s => s.PaymentProvider).HasMaxLength(50).IsRequired();
            b.Property(s => s.PaymentProviderCustomerRef).HasMaxLength(200);
            b.Property(s => s.PaymentProviderTokenRef).HasMaxLength(200);
            b.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict);
            b.HasQueryFilter(s => !s.IsDeleted);
        });

        builder.Entity<SubscriptionInvoice>(b =>
        {
            b.Property(i => i.PlanCode).HasMaxLength(50).IsRequired();
            b.Property(i => i.PlanNameAr).HasMaxLength(100).IsRequired();
            b.Property(i => i.Currency).HasMaxLength(10).IsRequired();
            b.Property(i => i.PaymentReference).HasMaxLength(200);
            b.Property(i => i.BaseAmount).HasPrecision(18, 2);
            b.Property(i => i.ExtraBranchesAmount).HasPrecision(18, 2);
            b.Property(i => i.ExtraUsersAmount).HasPrecision(18, 2);
            b.Property(i => i.MultiCompanyDiscountAmount).HasPrecision(18, 2);
            b.Property(i => i.TotalAmount).HasPrecision(18, 2);
            b.HasOne<Tenant>().WithMany().HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Subscription>().WithMany().HasForeignKey(i => i.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            b.HasQueryFilter(i => !i.IsDeleted);
        });

        builder.Entity<SupportTicket>(b =>
        {
            b.Property(t => t.TicketNumber).UseIdentityColumn(seed: 1000, increment: 1);
            b.HasIndex(t => t.TicketNumber).IsUnique();
            b.Property(t => t.CompanyCode).HasMaxLength(50).IsRequired();
            b.Property(t => t.RequesterEmail).HasMaxLength(200).IsRequired();
            b.Property(t => t.RequesterName).HasMaxLength(200).IsRequired();
            b.Property(t => t.Subject).HasMaxLength(300).IsRequired();
            b.HasMany(t => t.Messages).WithOne().HasForeignKey(m => m.TicketId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(t => !t.IsDeleted);
        });

        builder.Entity<SupportTicketMessage>(b =>
        {
            b.Property(m => m.SenderName).HasMaxLength(200).IsRequired();
            b.Property(m => m.Body).HasMaxLength(4000).IsRequired();
            b.HasMany(m => m.Attachments).WithOne().HasForeignKey(a => a.MessageId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(m => !m.IsDeleted);
        });

        builder.Entity<SupportTicketAttachment>(b =>
        {
            b.Property(a => a.FileName).HasMaxLength(260).IsRequired();
            b.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
            b.HasQueryFilter(a => !a.IsDeleted);
        });
    }
}
