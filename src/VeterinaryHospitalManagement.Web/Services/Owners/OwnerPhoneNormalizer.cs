using System.Text;

namespace VeterinaryHospitalManagement.Web.Services.Owners;

public sealed class OwnerPhoneNormalizer : IOwnerPhoneNormalizer
{
    private const string SupportedMobileLeadingDigits = "35789";

    public OwnerPhoneNormalizationResult Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return OwnerPhoneNormalizationResult.Failure(OwnerPhoneNormalizationErrorCodes.Missing);
        }

        var value = input.Trim();
        var digits = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (IsAsciiDigit(character))
            {
                digits.Append(character);
                continue;
            }

            if (character == '+' && index == 0)
            {
                digits.Append(character);
                continue;
            }

            if (IsSeparator(character)
                && index > 0
                && index < value.Length - 1
                && IsAsciiDigit(value[index - 1])
                && IsAsciiDigit(value[index + 1]))
            {
                continue;
            }

            return OwnerPhoneNormalizationResult.Failure(OwnerPhoneNormalizationErrorCodes.InvalidFormat);
        }

        var compact = digits.ToString();
        string subscriberNumber;

        if (compact.Length == 10 && compact[0] == '0')
        {
            subscriberNumber = compact[1..];
        }
        else if (compact.Length == 11 && compact.StartsWith("84", StringComparison.Ordinal))
        {
            subscriberNumber = compact[2..];
        }
        else if (compact.Length == 12 && compact.StartsWith("+84", StringComparison.Ordinal))
        {
            subscriberNumber = compact[3..];
        }
        else
        {
            return OwnerPhoneNormalizationResult.Failure(OwnerPhoneNormalizationErrorCodes.UnsupportedNumber);
        }

        if (subscriberNumber.Length != 9
            || !SupportedMobileLeadingDigits.Contains(subscriberNumber[0], StringComparison.Ordinal))
        {
            return OwnerPhoneNormalizationResult.Failure(OwnerPhoneNormalizationErrorCodes.UnsupportedNumber);
        }

        return OwnerPhoneNormalizationResult.Success($"+84{subscriberNumber}");
    }

    private static bool IsAsciiDigit(char character) => character is >= '0' and <= '9';

    private static bool IsSeparator(char character) => character is ' ' or '-' or '.';
}
