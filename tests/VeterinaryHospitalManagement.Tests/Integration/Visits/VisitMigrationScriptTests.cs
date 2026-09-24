using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VeterinaryHospitalManagement.Web.Data;

namespace VeterinaryHospitalManagement.Tests.Integration.Visits;

public sealed class VisitMigrationScriptTests
{
    [Fact]
    public void AddVisits_creates_locked_schema_and_rolls_back()
    {
        using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=localhost;Database=x;Integrated Security=True;TrustServerCertificate=True")
                .Options);

        var migrations = db.Database.GetMigrations().ToList();
        var before = Assert.Single(migrations, x => x.EndsWith("_AddAppointments"));
        var current = Assert.Single(migrations, x => x.EndsWith("_AddVisits"));
        var migrator = db.GetService<IMigrator>();

        var up = migrator.GenerateScript(before, current);
        Assert.Contains("CREATE TABLE [Visits]", up);
        Assert.Contains("CK_Visits_Waiting", up);
        Assert.Contains("CK_Visits_InProgress", up);
        Assert.Contains("CK_Visits_Completed", up);
        Assert.Contains("CK_Visits_Completed_Order", up);
        Assert.Contains("CK_Visits_Cancelled", up);
        Assert.Contains("CREATE SEQUENCE [VisitNumberSequence]", up);
        Assert.Contains("IX_Visits_Pet_ActiveStatus", up);
        Assert.Contains("IX_Visits_Vet_InProgress", up);
        Assert.Contains("ON DELETE NO ACTION", up);

        var down = migrator.GenerateScript(current, before);
        Assert.Contains("DROP TABLE [Visits]", down);
        Assert.Contains("DROP SEQUENCE [VisitNumberSequence]", down);
    }
}
