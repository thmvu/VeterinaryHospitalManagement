using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Tests.Unit.Catalogs;

public sealed class CatalogSchemaModelTests
{
    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=x;Integrated Security=True;TrustServerCertificate=True").Options);

    [Fact]
    public void ServiceCatalog_matches_approved_schema()
    {
        using var db = CreateContext();
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ServiceCatalog))!;

        Assert.Equal("ServiceCatalogs", entity.GetTableName());
        AssertProperty(entity, nameof(ServiceCatalog.Code), 30, true);
        AssertProperty(entity, nameof(ServiceCatalog.Name), 150, true);
        AssertProperty(entity, nameof(ServiceCatalog.Category), 100, true);
        AssertProperty(entity, nameof(ServiceCatalog.Description), 1000, false);
        Assert.Equal("decimal(18,2)", entity.FindProperty(nameof(ServiceCatalog.Price))!.GetColumnType());
        Assert.True(entity.FindProperty(nameof(ServiceCatalog.RowVersion))!.IsConcurrencyToken);
        AssertUniqueIndex(entity, nameof(ServiceCatalog.Code), "UQ_ServiceCatalogs_Code");
        Assert.Contains(entity.GetCheckConstraints(), x =>
            x.Name == "CK_ServiceCatalogs_Price" && x.Sql == "[Price] >= 0");
    }

    [Fact]
    public void Medicine_matches_approved_schema_and_has_no_sales_or_stock_fields()
    {
        using var db = CreateContext();
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Medicine))!;

        Assert.Equal("Medicines", entity.GetTableName());
        AssertProperty(entity, nameof(Medicine.Code), 30, true);
        AssertProperty(entity, nameof(Medicine.Name), 150, true);
        AssertProperty(entity, nameof(Medicine.ActiveIngredient), 200, false);
        AssertProperty(entity, nameof(Medicine.Strength), 100, false);
        AssertProperty(entity, nameof(Medicine.Unit), 50, true);
        Assert.True(entity.FindProperty(nameof(Medicine.RowVersion))!.IsConcurrencyToken);
        AssertUniqueIndex(entity, nameof(Medicine.Code), "UQ_Medicines_Code");
        Assert.Null(entity.FindProperty("Price"));
        Assert.Null(entity.FindProperty("UnitPrice"));
        Assert.Null(entity.FindProperty("Stock"));
        Assert.Null(entity.FindProperty("StockQuantity"));
    }

    private static void AssertProperty(IReadOnlyEntityType entity, string name, int length, bool required)
    {
        var property = entity.FindProperty(name)!;
        Assert.Equal(length, property.GetMaxLength());
        Assert.Equal(!required, property.IsNullable);
    }

    private static void AssertUniqueIndex(IReadOnlyEntityType entity, string propertyName, string databaseName)
    {
        var index = Assert.Single(entity.GetIndexes(), x =>
            x.Properties.Count == 1 && x.Properties[0].Name == propertyName);
        Assert.True(index.IsUnique);
        Assert.Equal(databaseName, index.GetDatabaseName());
    }
}
