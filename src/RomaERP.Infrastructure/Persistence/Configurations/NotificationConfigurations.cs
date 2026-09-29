using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomaERP.Domain.Common;

namespace RomaERP.Infrastructure.Persistence.Configurations;

public class WhatsAppCredentialConfiguration : IEntityTypeConfiguration<WhatsAppCredential>
{
    public void Configure(EntityTypeBuilder<WhatsAppCredential> builder)
    {
        builder.Property(c => c.PhoneNumberId).HasMaxLength(50).IsRequired();
        builder.Property(c => c.AccessTokenEncrypted).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.RecipientPhoneNumber).HasMaxLength(30).IsRequired();
        builder.Property(c => c.TemplateName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.TemplateLanguageCode).HasMaxLength(10).IsRequired();

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
