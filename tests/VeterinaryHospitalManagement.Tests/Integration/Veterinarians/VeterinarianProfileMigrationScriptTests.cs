using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Data;
namespace VeterinaryHospitalManagement.Tests.Integration.Veterinarians;
public sealed class VeterinarianProfileMigrationScriptTests
{
 [Fact] public void Migration_creates_profile_constraints()
 {
  using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=localhost;Database=x;Integrated Security=True;TrustServerCertificate=True").Options);
  var sql=db.Database.GenerateCreateScript(); Assert.Contains("CREATE TABLE [VeterinarianProfiles]",sql); Assert.Contains("[RowVersion] rowversion NOT NULL",sql); Assert.Contains("UNIQUE",sql); Assert.Contains("FK_VeterinarianProfiles_AspNetUsers_UserId",sql);
 }
}
