using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
namespace VeterinaryHospitalManagement.Tests.Unit.Scheduling;
public sealed class AppointmentSchemaModelTests
{
 [Fact]public void Appointment_matches_locked_schema(){using var db=Create();var e=db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Appointment))!;Assert.Equal("Appointments",e.GetTableName());Assert.Equal(30,e.FindProperty("AppointmentNumber")!.GetMaxLength());Assert.Equal(500,e.FindProperty("Reason")!.GetMaxLength());Assert.Equal(20,e.FindProperty("Status")!.GetMaxLength());Assert.Equal(500,e.FindProperty("CancellationReason")!.GetMaxLength());Assert.Equal("datetimeoffset(7)",e.FindProperty("StartAt")!.GetColumnType());Assert.True(e.FindProperty("RowVersion")!.IsConcurrencyToken);Assert.Contains(e.GetCheckConstraints(),x=>x.Name=="CK_Appointments_TimeRange");Assert.Contains(e.GetCheckConstraints(),x=>x.Name=="CK_Appointments_Status");Assert.Contains(e.GetIndexes(),x=>x.IsUnique&&x.Properties.Single().Name=="AppointmentNumber");Assert.Equal(3,e.GetForeignKeys().Count());Assert.All(e.GetForeignKeys(),x=>Assert.Equal(DeleteBehavior.Restrict,x.DeleteBehavior));}
 private static ApplicationDbContext Create()=>new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=localhost;Database=x;Integrated Security=True;TrustServerCertificate=True").Options);
}
