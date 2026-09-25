using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Tests.Unit.Visits;

public sealed class VisitSchemaModelTests
{
    [Fact]
    public void Visit_matches_locked_schema()
    {
        using var db = Create();
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Visit))!;

        Assert.Equal("Visits", entity.GetTableName());
        Assert.Equal(20, entity.FindProperty("VisitNumber")!.GetMaxLength());
        Assert.Equal(20, entity.FindProperty("Status")!.GetMaxLength());
        Assert.Equal(100, entity.FindProperty("PetNameSnapshot")!.GetMaxLength());
        Assert.Equal(150, entity.FindProperty("OwnerNameSnapshot")!.GetMaxLength());
        Assert.Equal(20, entity.FindProperty("OwnerPhoneSnapshot")!.GetMaxLength());
        Assert.Equal(150, entity.FindProperty("VeterinarianNameSnapshot")!.GetMaxLength());
        Assert.Equal(500, entity.FindProperty("CancellationReason")!.GetMaxLength());
        Assert.Equal(450, entity.FindProperty("CheckedInByUserId")!.GetMaxLength());
        Assert.True(entity.FindProperty("RowVersion")!.IsConcurrencyToken);

        // Check constraints
        var constraints = entity.GetCheckConstraints().Select(c => c.Name).ToList();
        Assert.Contains("CK_Visits_Waiting", constraints);
        Assert.Contains("CK_Visits_InProgress", constraints);
        Assert.Contains("CK_Visits_Completed", constraints);
        Assert.Contains("CK_Visits_Completed_Order", constraints);
        Assert.Contains("CK_Visits_Cancelled", constraints);

        // Filtered unique indexes
        var indexes = entity.GetIndexes().ToList();
        Assert.Contains(indexes, i => i.IsUnique && i.Properties.Any(p => p.Name == "VisitNumber"));
        Assert.Contains(indexes, i => i.IsUnique && i.Properties.Any(p => p.Name == "AppointmentId") && i.GetFilter() == "[AppointmentId] IS NOT NULL");
        Assert.Contains(indexes, i => i.IsUnique && i.Properties.Any(p => p.Name == "PetId") && i.GetFilter() == "[Status] IN ('Waiting', 'InProgress')");
        Assert.Contains(indexes, i => i.IsUnique && i.Properties.Any(p => p.Name == "VeterinarianId") && i.GetFilter() == "[Status] = 'InProgress'");

        // Foreign keys
        Assert.Equal(4, entity.GetForeignKeys().Count());
        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    private static ApplicationDbContext Create() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=x;Integrated Security=True;TrustServerCertificate=True")
            .Options);
}
