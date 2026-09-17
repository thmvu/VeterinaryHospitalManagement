namespace VeterinaryHospitalManagement.Web.Services.Pets;

public static class PetRules
{
    public const string PetCodePrefix = "PET-";
    public const long MaxPetCodeSequenceValue = 999999;

    public static string FormatPetCode(long sequenceValue)
    {
        if (sequenceValue is < 1 or > MaxPetCodeSequenceValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceValue),
                "Pet code sequence values must stay between 1 and 999999.");
        }

        return $"{PetCodePrefix}{sequenceValue:000000}";
    }

    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PetManagementException("Tên thú cưng là bắt buộc.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length > 100)
        {
            throw new PetManagementException("Tên thú cưng không được vượt quá 100 ký tự.");
        }

        return trimmed;
    }

    public static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new PetManagementException($"{fieldName} không được vượt quá {maxLength} ký tự.");
        }

        return trimmed;
    }

    public static void ValidateBirthDate(DateOnly? birthDate, DateOnly today)
    {
        if (birthDate.HasValue && birthDate.Value > today)
        {
            throw new PetManagementException("Ngày sinh thú cưng không được vượt quá ngày hiện tại.");
        }
    }
}
