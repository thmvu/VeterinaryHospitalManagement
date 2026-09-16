namespace VeterinaryHospitalManagement.Web.Services.Owners;

public sealed class OwnerPhoneNormalizationResult
{
    private OwnerPhoneNormalizationResult(bool isValid, string? canonicalNumber, string? errorCode)
    {
        IsValid = isValid;
        CanonicalNumber = canonicalNumber;
        ErrorCode = errorCode;
    }

    public bool IsValid { get; }

    public string? CanonicalNumber { get; }

    public string? ErrorCode { get; }

    public static OwnerPhoneNormalizationResult Success(string canonicalNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalNumber);
        return new OwnerPhoneNormalizationResult(true, canonicalNumber, null);
    }

    public static OwnerPhoneNormalizationResult Failure(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        return new OwnerPhoneNormalizationResult(false, null, errorCode);
    }
}

public static class OwnerPhoneNormalizationErrorCodes
{
    public const string Missing = "Phone.Missing";
    public const string InvalidFormat = "Phone.InvalidFormat";
    public const string UnsupportedNumber = "Phone.UnsupportedNumber";
}
