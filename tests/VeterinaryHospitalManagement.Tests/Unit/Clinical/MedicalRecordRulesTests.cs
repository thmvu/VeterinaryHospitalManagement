using VeterinaryHospitalManagement.Web.Services.Clinical;

namespace VeterinaryHospitalManagement.Tests.Unit.Clinical;

public sealed class MedicalRecordRulesTests
{
    [Fact]
    public void Chief_complaint_is_trimmed_and_cannot_be_blank()
    {
        Assert.Equal("Bỏ ăn", MedicalRecordRules.NormalizeChiefComplaint("  Bỏ ăn  "));
        Assert.Throws<MedicalRecordManagementException>(() => MedicalRecordRules.NormalizeChiefComplaint("  "));
    }

    [Fact]
    public void Measurements_reject_invalid_weight_and_excess_decimal_places()
    {
        Assert.Throws<MedicalRecordManagementException>(() => MedicalRecordRules.ValidateMeasurements(0m, null));
        Assert.Throws<MedicalRecordManagementException>(() => MedicalRecordRules.ValidateMeasurements(8.123m, null));
        Assert.Throws<MedicalRecordManagementException>(() => MedicalRecordRules.ValidateMeasurements(null, 38.55m));
        MedicalRecordRules.ValidateMeasurements(8.25m, 38.5m);
    }
}
