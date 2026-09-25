using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Tests.Unit.Clinical;

public sealed class ClinicalSchemaModelTests
{
    [Fact]
    public void One_medical_record_and_prescription_per_visit_but_repeated_drugs_and_services_are_allowed()
    {
        using var db = Create();
        var model = db.GetService<IDesignTimeModel>().Model;
        var records = model.FindEntityType(typeof(MedicalRecord))!;
        var prescriptions = model.FindEntityType(typeof(Prescription))!;
        var items = model.FindEntityType(typeof(PrescriptionItem))!;
        var services = model.FindEntityType(typeof(VisitService))!;

        Assert.Contains(records.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(MedicalRecord.VisitId));
        Assert.Contains(prescriptions.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(Prescription.VisitId));
        Assert.DoesNotContain(items.GetIndexes(), x => x.IsUnique && x.Properties.Any(p => p.Name == nameof(PrescriptionItem.MedicineId)));
        Assert.DoesNotContain(services.GetIndexes(), x => x.IsUnique && x.Properties.Any(p => p.Name == nameof(VisitService.ServiceCatalogId)));
        Assert.All(new[] { records, prescriptions, services }, x =>
            Assert.True(x.FindProperty("RowVersion")!.IsConcurrencyToken));
        Assert.Equal("decimal(6,2)", records.FindProperty(nameof(MedicalRecord.WeightKg))!.GetColumnType());
        Assert.Equal("decimal(10,2)", items.FindProperty(nameof(PrescriptionItem.Quantity))!.GetColumnType());
        Assert.Equal("decimal(18,2)", services.FindProperty(nameof(VisitService.UnitPrice))!.GetColumnType());
    }

    private static ApplicationDbContext Create() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=x;Integrated Security=True;TrustServerCertificate=True")
            .Options);
}
