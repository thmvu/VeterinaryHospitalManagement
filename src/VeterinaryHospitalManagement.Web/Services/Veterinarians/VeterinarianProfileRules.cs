namespace VeterinaryHospitalManagement.Web.Services.Veterinarians;

public static class VeterinarianProfileRules
{
    public static string FormatDoctorCode(long sequenceValue)
    {
        if (sequenceValue is < 1 or > 999999) throw new ArgumentOutOfRangeException(nameof(sequenceValue));
        return $"VET-{sequenceValue:000000}";
    }
    public static string NormalizeDoctorCode(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length is 0 or > 30) throw new VeterinarianManagementException("Mã bác sĩ là bắt buộc và tối đa 30 ký tự.");
        return normalized;
    }

    public static string? NormalizeSpecialty(string? value)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > 150) throw new VeterinarianManagementException("Chuyên khoa tối đa 150 ký tự.");
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
