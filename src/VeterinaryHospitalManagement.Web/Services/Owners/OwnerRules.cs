namespace VeterinaryHospitalManagement.Web.Services.Owners;

public static class OwnerRules
{
    public const string OwnerCodePrefix = "OWN-";
    public const long MaxOwnerCodeSequenceValue = 999999;

    public static string FormatOwnerCode(long sequenceValue)
    {
        if (sequenceValue is < 1 or > MaxOwnerCodeSequenceValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceValue),
                "Owner code sequence values must stay between 1 and 999999.");
        }

        return $"{OwnerCodePrefix}{sequenceValue:000000}";
    }

    public static string DescribePhoneError(string? errorCode) => errorCode switch
    {
        OwnerPhoneNormalizationErrorCodes.Missing => "Số điện thoại là bắt buộc.",
        OwnerPhoneNormalizationErrorCodes.InvalidFormat =>
            "Số điện thoại chỉ nhận chữ số; khoảng trắng, dấu gạch ngang hoặc dấu chấm chỉ được nằm giữa các nhóm chữ số.",
        OwnerPhoneNormalizationErrorCodes.UnsupportedNumber =>
            "Số điện thoại phải là số di động Việt Nam hợp lệ (0 hoặc +84/84 theo sau 9 chữ số, đầu số 3/5/7/8/9).",
        _ => "Số điện thoại không hợp lệ."
    };
}
