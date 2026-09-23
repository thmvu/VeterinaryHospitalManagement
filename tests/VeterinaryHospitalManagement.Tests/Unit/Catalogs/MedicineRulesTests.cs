using VeterinaryHospitalManagement.Web.Services.Catalogs;

namespace VeterinaryHospitalManagement.Tests.Unit.Catalogs;

public sealed class MedicineRulesTests
{
    [Fact]
    public void Normalize_trims_uppercases_code_and_clears_optional_fields()
    {
        Assert.Equal("MED-01", MedicineRules.NormalizeCode(" med-01 "));
        Assert.Equal("Amoxicillin", MedicineRules.NormalizeName(" Amoxicillin "));
        Assert.Equal("viên", MedicineRules.NormalizeUnit(" viên "));
        Assert.Null(MedicineRules.NormalizeActiveIngredient("  "));
        Assert.Null(MedicineRules.NormalizeStrength(null));
    }

    [Fact]
    public void Normalize_rejects_missing_or_overlong_values()
    {
        Assert.Throws<MedicineManagementException>(() => MedicineRules.NormalizeCode(""));
        Assert.Throws<MedicineManagementException>(() => MedicineRules.NormalizeName(new string('A', 151)));
        Assert.Throws<MedicineManagementException>(() => MedicineRules.NormalizeUnit(new string('A', 51)));
        Assert.Throws<MedicineManagementException>(() => MedicineRules.NormalizeActiveIngredient(new string('A', 201)));
        Assert.Throws<MedicineManagementException>(() => MedicineRules.NormalizeStrength(new string('A', 101)));
    }
}
