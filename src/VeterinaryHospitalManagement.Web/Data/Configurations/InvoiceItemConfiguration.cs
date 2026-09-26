using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("InvoiceItems", table =>
        {
            table.HasCheckConstraint("CK_InvoiceItems_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_InvoiceItems_UnitPrice", "[UnitPrice] >= 0 AND [UnitPrice] = FLOOR([UnitPrice])");
            table.HasCheckConstraint("CK_InvoiceItems_LineTotal", "[LineTotal] >= 0 AND [LineTotal] = FLOOR([LineTotal])");
        });
        builder.Property(x => x.DescriptionSnapshot).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Quantity).HasColumnType("decimal(10,2)");
        builder.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
        builder.Property(x => x.LineTotal).HasColumnType("decimal(18,2)");
        builder.HasIndex(x => x.VisitServiceId).IsUnique();
        builder.HasOne(x => x.Invoice).WithMany(x => x.Items).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.VisitService).WithOne().HasForeignKey<InvoiceItem>(x => x.VisitServiceId).OnDelete(DeleteBehavior.Restrict);
    }
}
