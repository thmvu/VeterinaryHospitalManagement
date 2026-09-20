using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using VeterinaryHospitalManagement.Web.Data;
namespace VeterinaryHospitalManagement.Tests.Unit.Veterinarians;
public sealed class VeterinarianProfileSchemaModelTests
{
 [Fact] public void Profile_has_unique_code_user_restrict_fk_and_rowversion()
 {
  using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=localhost;Database=x;Integrated Security=True;TrustServerCertificate=True").Options);
  var entity=db.GetService<IDesignTimeModel>().Model.FindEntityType("VeterinaryHospitalManagement.Web.Models.Entities.VeterinarianProfile"); Assert.NotNull(entity); Assert.Equal("VeterinarianProfiles",entity!.GetTableName());
  Assert.Equal(30,entity.FindProperty("DoctorCode")!.GetMaxLength()); Assert.Equal(150,entity.FindProperty("Specialty")!.GetMaxLength());
  Assert.Contains(entity.GetIndexes(),x=>x.IsUnique&&x.Properties.Single().Name=="UserId"); Assert.Contains(entity.GetIndexes(),x=>x.IsUnique&&x.Properties.Single().Name=="DoctorCode");
  var fk=Assert.Single(entity.GetForeignKeys()); Assert.Equal(DeleteBehavior.Restrict,fk.DeleteBehavior);
  var rv=entity.FindProperty("RowVersion")!; Assert.True(rv.IsConcurrencyToken); Assert.Equal(ValueGenerated.OnAddOrUpdate,rv.ValueGenerated); Assert.Equal("rowversion",rv.GetColumnType());
 }
}
