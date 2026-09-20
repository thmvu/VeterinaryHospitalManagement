using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VeterinaryHospitalManagement.Web.Data;
namespace VeterinaryHospitalManagement.Tests.Integration.Veterinarians;
public sealed class VeterinarianProfileMigrationScriptTests
{
 [Fact] public void AddVeterinarianProfiles_generates_exact_schema_and_down_up_scripts()
 {
  using var db=Create();var migrations=db.Database.GetMigrations().ToList();var before=Assert.Single(migrations,x=>x.EndsWith("_AddOwnersAndPets"));var current=Assert.Single(migrations,x=>x.EndsWith("_AddVeterinarianProfiles"));var migrator=db.GetService<IMigrator>();
  var up=migrator.GenerateScript(before,current);Assert.Contains("CREATE TABLE [VeterinarianProfiles]",up);Assert.Contains("[RowVersion] rowversion NOT NULL",up);Assert.Contains("CREATE UNIQUE INDEX [IX_VeterinarianProfiles_DoctorCode]",up);Assert.Contains("CREATE UNIQUE INDEX [IX_VeterinarianProfiles_UserId]",up);Assert.Contains("CONSTRAINT [FK_VeterinarianProfiles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION",up);
  var down=migrator.GenerateScript(current,before);Assert.Contains("DROP TABLE [VeterinarianProfiles]",down);Assert.Contains("CREATE TABLE [VeterinarianProfiles]",migrator.GenerateScript(before,current));
 }
 private static ApplicationDbContext Create()=>new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=localhost;Database=x;Integrated Security=True;TrustServerCertificate=True").Options);
}
