using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;
namespace VeterinaryHospitalManagement.Web.Data.Configurations;
public sealed class AppointmentConfiguration:IEntityTypeConfiguration<Appointment>
{
 public void Configure(EntityTypeBuilder<Appointment>b)
 {
  b.ToTable("Appointments",t=>{t.HasCheckConstraint("CK_Appointments_TimeRange","[StartAt] < [EndAt]");t.HasCheckConstraint("CK_Appointments_Status","[Status] IN ('Scheduled','CheckedIn','Cancelled','NoShow')");t.HasCheckConstraint("CK_Appointments_CancellationReason","([Status] = 'Cancelled' AND [CancellationReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CancellationReason]))) > 0) OR ([Status] <> 'Cancelled' AND [CancellationReason] IS NULL)");});
  b.Property(x=>x.AppointmentNumber).HasMaxLength(30).IsRequired();b.HasIndex(x=>x.AppointmentNumber).IsUnique().HasDatabaseName("UQ_Appointments_AppointmentNumber");
  b.Property(x=>x.StartAt).HasColumnType("datetimeoffset(7)").IsRequired();b.Property(x=>x.EndAt).HasColumnType("datetimeoffset(7)").IsRequired();b.Property(x=>x.Reason).HasMaxLength(500).IsRequired();b.Property(x=>x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();b.Property(x=>x.CancellationReason).HasMaxLength(500);b.Property(x=>x.CreatedAt).HasColumnType("datetimeoffset(7)").IsRequired();b.Property(x=>x.RowVersion).IsRowVersion().HasColumnType("rowversion");
  b.HasIndex(x=>new{x.VeterinarianId,x.Status,x.StartAt}).HasDatabaseName("IX_Appointments_VeterinarianId_Status_StartAt").IncludeProperties(x=>new{x.EndAt,x.PetId});
  b.HasIndex(x=>new{x.PetId,x.Status,x.StartAt}).HasDatabaseName("IX_Appointments_PetId_Status_StartAt").IncludeProperties(x=>new{x.EndAt,x.VeterinarianId});
  b.HasOne(x=>x.Pet).WithMany(x=>x.Appointments).HasForeignKey(x=>x.PetId).OnDelete(DeleteBehavior.Restrict);b.HasOne(x=>x.Veterinarian).WithMany(x=>x.Appointments).HasForeignKey(x=>x.VeterinarianId).OnDelete(DeleteBehavior.Restrict);b.HasOne(x=>x.CreatedByUser).WithMany().HasForeignKey(x=>x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
 }
}
