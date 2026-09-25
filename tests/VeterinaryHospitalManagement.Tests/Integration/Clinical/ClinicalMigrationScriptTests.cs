using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VeterinaryHospitalManagement.Web.Data;

namespace VeterinaryHospitalManagement.Tests.Integration.Clinical;

public sealed class ClinicalMigrationScriptTests
{
    [Fact]
    public void AddClinicalRecords_creates_four_tables_with_status_and_quantity_constraints()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=x;Integrated Security=True;TrustServerCertificate=True").Options);
        var migrations = db.Database.GetMigrations().ToList();
        var before = Assert.Single(migrations, x => x.EndsWith("_ExpandVisitNameSnapshots"));
        var current = Assert.Single(migrations, x => x.EndsWith("_AddClinicalRecords"));
        var migrator = db.GetService<IMigrator>();
        var up = migrator.GenerateScript(before, current);

        foreach (var table in new[] { "MedicalRecords", "Prescriptions", "PrescriptionItems", "VisitServices" })
            Assert.Contains($"CREATE TABLE [{table}]", up);
        Assert.Contains("CK_MedicalRecords_WeightKg", up);
        Assert.Contains("CK_MedicalRecords_Status", up);
        Assert.Contains("CK_Prescriptions_Status", up);
        Assert.Contains("CK_PrescriptionItems_Quantity", up);
        Assert.Contains("CK_VisitServices_Status", up);
        Assert.Contains("CK_VisitServices_Quantity", up);
        Assert.Contains("ON DELETE NO ACTION", up);

        var down = migrator.GenerateScript(current, before);
        foreach (var table in new[] { "MedicalRecords", "Prescriptions", "PrescriptionItems", "VisitServices" })
            Assert.Contains($"DROP TABLE [{table}]", down);
    }
}
