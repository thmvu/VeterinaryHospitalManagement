using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VeterinaryHospitalManagement.Web.Data;

namespace VeterinaryHospitalManagement.Tests.Integration.Catalogs;

public sealed class CatalogMigrationScriptTests
{
    [Fact]
    public void AddCatalogs_generates_approved_tables_constraints_and_rollback()
    {
        using var db = Create();
        var migrations = db.Database.GetMigrations().ToList();
        var before = Assert.Single(migrations, x => x.EndsWith("_AddVeterinarianShifts"));
        var current = Assert.Single(migrations, x => x.EndsWith("_AddCatalogs"));
        var migrator = db.GetService<IMigrator>();

        var up = migrator.GenerateScript(before, current);
        Assert.Contains("CREATE TABLE [Medicines]", up);
        Assert.Contains("[Code] nvarchar(30) NOT NULL", up);
        Assert.Contains("[ActiveIngredient] nvarchar(200) NULL", up);
        Assert.Contains("[Strength] nvarchar(100) NULL", up);
        Assert.Contains("CREATE UNIQUE INDEX [UQ_Medicines_Code]", up);
        Assert.Contains("CREATE TABLE [ServiceCatalogs]", up);
        Assert.Contains("[Category] nvarchar(100) NOT NULL", up);
        Assert.Contains("[Price] decimal(18,2) NOT NULL", up);
        Assert.Contains("CK_ServiceCatalogs_Price", up);
        Assert.Contains("[Price] >= 0", up);
        Assert.Contains("CREATE UNIQUE INDEX [UQ_ServiceCatalogs_Code]", up);

        var down = migrator.GenerateScript(current, before);
        Assert.Contains("DROP TABLE [Medicines]", down);
        Assert.Contains("DROP TABLE [ServiceCatalogs]", down);
    }

    private static ApplicationDbContext Create() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=x;Integrated Security=True;TrustServerCertificate=True").Options);
}
