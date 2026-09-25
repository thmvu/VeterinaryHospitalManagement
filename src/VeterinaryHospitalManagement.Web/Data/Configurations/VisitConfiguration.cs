using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public sealed class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        builder.ToTable("Visits", t =>
        {
            // ── CHECK tuple hai chiều theo trạng thái ────────────────────────────
            // Waiting: chỉ có CheckedInAt, chưa có StartedAt/CompletedAt/reason
            t.HasCheckConstraint("CK_Visits_Waiting",
                "NOT(Status = 'Waiting' AND (StartedAt IS NOT NULL OR CompletedAt IS NOT NULL OR CancellationReason IS NOT NULL))");

            // InProgress: có StartedAt, chưa có CompletedAt/reason
            t.HasCheckConstraint("CK_Visits_InProgress",
                "NOT(Status = 'InProgress' AND (StartedAt IS NULL OR CompletedAt IS NOT NULL OR CancellationReason IS NOT NULL))");

            // Completed: có StartedAt + CompletedAt theo thứ tự, không có reason
            t.HasCheckConstraint("CK_Visits_Completed",
                "NOT(Status = 'Completed' AND (StartedAt IS NULL OR CompletedAt IS NULL OR CancellationReason IS NOT NULL))");
            t.HasCheckConstraint("CK_Visits_Completed_Order",
                "NOT(Status = 'Completed' AND CompletedAt IS NOT NULL AND StartedAt IS NOT NULL AND CompletedAt <= StartedAt)");

            // Cancelled: có reason, chưa có StartedAt/CompletedAt
            t.HasCheckConstraint("CK_Visits_Cancelled",
                "NOT(Status = 'Cancelled' AND (CancellationReason IS NULL OR StartedAt IS NOT NULL OR CompletedAt IS NOT NULL))");
        });

        builder.HasKey(v => v.Id);

        builder.Property(v => v.VisitNumber)
            .IsRequired()
            .HasMaxLength(20);
        builder.HasIndex(v => v.VisitNumber).IsUnique();

        builder.Property(v => v.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(v => v.PetNameSnapshot).IsRequired().HasMaxLength(100);
        builder.Property(v => v.OwnerNameSnapshot).IsRequired().HasMaxLength(150);
        builder.Property(v => v.OwnerPhoneSnapshot).IsRequired().HasMaxLength(20);
        builder.Property(v => v.VeterinarianNameSnapshot).IsRequired().HasMaxLength(150);

        builder.Property(v => v.CancellationReason).HasMaxLength(500);
        builder.Property(v => v.CheckedInByUserId).IsRequired().HasMaxLength(450);

        builder.Property(v => v.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // ── FK Appointment (filtered unique: tối đa 1 Visit/Appointment) ──────
        builder.HasOne(v => v.Appointment)
            .WithOne(a => a.Visit)
            .HasForeignKey<Visit>(v => v.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => v.AppointmentId)
            .IsUnique()
            .HasFilter("[AppointmentId] IS NOT NULL");

        // ── FK Pet ─────────────────────────────────────────────────────────────
        builder.HasOne(v => v.Pet)
            .WithMany()
            .HasForeignKey(v => v.PetId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── FK VeterinarianProfile ──────────────────────────────────────────────
        builder.HasOne(v => v.Veterinarian)
            .WithMany()
            .HasForeignKey(v => v.VeterinarianId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── FK CheckedInByUser ──────────────────────────────────────────────────
        builder.HasOne(v => v.CheckedInByUser)
            .WithMany()
            .HasForeignKey(v => v.CheckedInByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Index hàng đợi: Pet đang mở (Waiting/InProgress) ───────────────────
        // Filtered unique: một Pet tối đa 1 Waiting/InProgress tại cùng thời điểm
        builder.HasIndex(v => v.PetId)
            .IsUnique()
            .HasFilter("[Status] IN ('Waiting', 'InProgress')")
            .HasDatabaseName("IX_Visits_Pet_ActiveStatus");

        // Filtered unique: một bác sĩ tối đa 1 InProgress
        builder.HasIndex(v => v.VeterinarianId)
            .IsUnique()
            .HasFilter("[Status] = 'InProgress'")
            .HasDatabaseName("IX_Visits_Vet_InProgress");
    }
}
