namespace VeterinaryHospitalManagement.Web.Services.Clinical;

public static class MedicalRecordRules
{
    public static string NormalizeChiefComplaint(string? value)
    {
        var result = value?.Trim() ?? string.Empty;
        if (result.Length == 0) throw new MedicalRecordManagementException("Lý do khám là bắt buộc.");
        if (result.Length > 2000) throw new MedicalRecordManagementException("Lý do khám không được vượt quá 2000 ký tự.");
        return result;
    }

    public static void ValidateMeasurements(decimal? weightKg, decimal? temperatureC)
    {
        if (weightKg is <= 0 or > 9999.99m || (weightKg.HasValue && decimal.Round(weightKg.Value, 2) != weightKg.Value))
            throw new MedicalRecordManagementException("Cân nặng phải lớn hơn 0 và có tối đa 2 chữ số thập phân.");
        if (temperatureC is < -999.9m or > 999.9m || (temperatureC.HasValue && decimal.Round(temperatureC.Value, 1) != temperatureC.Value))
            throw new MedicalRecordManagementException("Nhiệt độ có tối đa 1 chữ số thập phân.");
    }
}
