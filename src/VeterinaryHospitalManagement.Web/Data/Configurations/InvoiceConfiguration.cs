using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", table =>
        {
            table.HasCheckConstraint("CK_Invoices_PaymentMethod", "[PaymentMethod] IN ('Cash', 'BankTransfer')");
            table.HasCheckConstraint("CK_Invoices_TotalAmount", "[TotalAmount] >= 0 AND [TotalAmount] = FLOOR([TotalAmount])");
        });
        builder.Property(x => x.InvoiceNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.OwnerNameSnapshot).HasMaxLength(150).IsRequired();
        builder.Property(x => x.OwnerPhoneSnapshot).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PetNameSnapshot).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.PaidAt).HasColumnType("datetimeoffset(7)");
        builder.Property(x => x.ProcessedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.ProcessedByNameSnapshot).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();
        builder.HasIndex(x => x.VisitId).IsUnique();
        builder.HasIndex(x => x.PaidAt);
        builder.HasOne(x => x.Visit).WithOne(x => x.Invoice).HasForeignKey<Invoice>(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProcessedByUser).WithMany().HasForeignKey(x => x.ProcessedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
