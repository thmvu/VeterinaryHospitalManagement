using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable(
            "AuditLogs",
            table => table.HasCheckConstraint(
                "CK_AuditLogs_ActorType",
                "[ActorType] IN (N'Internal', N'System')"));

        builder.Property(auditLog => auditLog.Id)
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(auditLog => auditLog.ActorType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(auditLog => auditLog.UserId)
            .HasMaxLength(450);

        builder.Property(auditLog => auditLog.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(auditLog => auditLog.EntityName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(auditLog => auditLog.EntityId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(auditLog => auditLog.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(auditLog => auditLog.CreatedAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.HasOne(auditLog => auditLog.User)
            .WithMany()
            .HasForeignKey(auditLog => auditLog.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(auditLog => auditLog.CreatedAt);
    }
}
