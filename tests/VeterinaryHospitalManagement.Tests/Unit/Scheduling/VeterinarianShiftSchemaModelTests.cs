using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using VeterinaryHospitalManagement.Web.Data;

namespace VeterinaryHospitalManagement.Tests.Unit.Scheduling;

public sealed class VeterinarianShiftSchemaModelTests
{
    [Fact]
    public void Shift_has_correct_table_columns_index_fk_and_rowversion()
    {
        using var db = Create();
        var model = db.GetService<IDesignTimeModel>().Model;

        var entity = model.FindEntityType(
            "VeterinaryHospitalManagement.Web.Models.Entities.VeterinarianShift");
        Assert.NotNull(entity);
        Assert.Equal("VeterinarianShifts", entity!.GetTableName());

        // Columns
        var startAt = entity.FindProperty("StartAt");
        Assert.NotNull(startAt);
        Assert.Equal("datetimeoffset(7)", startAt!.GetColumnType());
        Assert.False(startAt.IsNullable);

        var endAt = entity.FindProperty("EndAt");
        Assert.NotNull(endAt);
        Assert.Equal("datetimeoffset(7)", endAt!.GetColumnType());
        Assert.False(endAt.IsNullable);

        var isActive = entity.FindProperty("IsActive");
        Assert.NotNull(isActive);
        Assert.False(isActive!.IsNullable);

        // RowVersion
        var rv = entity.FindProperty("RowVersion")!;
        Assert.True(rv.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rv.ValueGenerated);
        Assert.Equal("rowversion", rv.GetColumnType());

        // FK to VeterinarianProfiles with Restrict
        var fk = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.Equal("VeterinarianId", fk.Properties.Single().Name);

        // Composite index with INCLUDE EndAt
        var idx = Assert.Single(entity.GetIndexes(), x =>
            x.Properties.Any(p => p.Name == "VeterinarianId")
            && x.Properties.Any(p => p.Name == "IsActive")
            && x.Properties.Any(p => p.Name == "StartAt"));
        Assert.NotNull(idx);

        // CHECK constraint
        var storeObject = StoreObjectIdentifier.Table("VeterinarianShifts");
        var check = entity.GetCheckConstraints().SingleOrDefault(c =>
            c.Name == "CK_VeterinarianShifts_TimeRange");
        Assert.NotNull(check);
        Assert.Contains("[StartAt] < [EndAt]", check!.Sql);
    }

    private static ApplicationDbContext Create() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=x;Integrated Security=True;TrustServerCertificate=True")
            .Options);
}
