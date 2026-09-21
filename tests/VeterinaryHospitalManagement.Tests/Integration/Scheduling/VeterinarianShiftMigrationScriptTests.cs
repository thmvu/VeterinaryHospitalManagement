using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VeterinaryHospitalManagement.Web.Data;

namespace VeterinaryHospitalManagement.Tests.Integration.Scheduling;

public sealed class VeterinarianShiftMigrationScriptTests
{
    [Fact]
    public void AddVeterinarianShifts_generates_exact_schema_and_down_up_scripts()
    {
        using var db = Create();
        var migrations = db.Database.GetMigrations().ToList();

        var before = Assert.Single(migrations, x => x.EndsWith("_AddVeterinarianProfiles"));
        var current = Assert.Single(migrations, x => x.EndsWith("_AddVeterinarianShifts"));

        var migrator = db.GetService<IMigrator>();

        // Up script
        var up = migrator.GenerateScript(before, current);
        Assert.Contains("CREATE TABLE [VeterinarianShifts]", up);
        Assert.Contains("[StartAt] datetimeoffset(7) NOT NULL", up);
        Assert.Contains("[EndAt] datetimeoffset(7) NOT NULL", up);
        Assert.Contains("[RowVersion] rowversion NOT NULL", up);
        Assert.Contains("CK_VeterinarianShifts_TimeRange", up);
        Assert.Contains("[StartAt] < [EndAt]", up);
        Assert.Contains(
            "CONSTRAINT [FK_VeterinarianShifts_VeterinarianProfiles_VeterinarianId] FOREIGN KEY ([VeterinarianId]) REFERENCES [VeterinarianProfiles] ([Id]) ON DELETE NO ACTION",
            up);
        Assert.Contains("CREATE INDEX [IX_VeterinarianShifts_VeterinarianId_IsActive_StartAt]", up);
        Assert.Contains("INCLUDE ([EndAt])", up);

        // Down script
        var down = migrator.GenerateScript(current, before);
        Assert.Contains("DROP TABLE [VeterinarianShifts]", down);

        // Down then up produces same CREATE TABLE
        Assert.Contains("CREATE TABLE [VeterinarianShifts]", migrator.GenerateScript(before, current));
    }

    private static ApplicationDbContext Create() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=x;Integrated Security=True;TrustServerCertificate=True")
            .Options);
}
