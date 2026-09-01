using CustomerPortal.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerPortal.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customer", t =>
            t.HasCheckConstraint("ck_customer_role", "role IN ('CUSTOMER','ADMIN')"));

        builder.HasKey(c => c.Id).HasName("pk_customer");

        builder.Property(c => c.Email)
            .HasMaxLength(254)
            .IsRequired();
        builder.HasIndex(c => c.Email)
            .IsUnique()
            .HasDatabaseName("uq_customer_email");

        builder.Property(c => c.PasswordHash)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(c => c.Role)
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("CUSTOMER");

        builder.Property(c => c.Enabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();
    }
}
