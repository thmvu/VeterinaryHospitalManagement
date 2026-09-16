using VeterinaryHospitalManagement.Web.Services.Owners;

namespace VeterinaryHospitalManagement.Tests.Unit.Owners;

public sealed class OwnerPhoneNormalizerTests
{
    private readonly OwnerPhoneNormalizer normalizer = new();

    [Theory]
    [InlineData("0912345678")]
    [InlineData("84912345678")]
    [InlineData("+84912345678")]
    [InlineData("  0912345678  ")]
    [InlineData("0912 345 678")]
    [InlineData("0912-345-678")]
    [InlineData("0912.345.678")]
    [InlineData("0912-345.678")]
    [InlineData("84 912 345 678")]
    [InlineData("+84 912 345 678")]
    public void Normalize_accepts_supported_forms_and_returns_canonical_number(string input)
    {
        var result = normalizer.Normalize(input);

        Assert.True(result.IsValid);
        Assert.Equal("+84912345678", result.CanonicalNumber);
        Assert.Null(result.ErrorCode);
    }

    [Theory]
    [InlineData("0312345678", "+84312345678")]
    [InlineData("0512345678", "+84512345678")]
    [InlineData("0712345678", "+84712345678")]
    [InlineData("0812345678", "+84812345678")]
    [InlineData("0912345678", "+84912345678")]
    public void Normalize_accepts_each_supported_mobile_leading_digit(
        string input,
        string expectedCanonicalNumber)
    {
        var result = normalizer.Normalize(input);

        Assert.True(result.IsValid);
        Assert.Equal(expectedCanonicalNumber, result.CanonicalNumber);
        Assert.Null(result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_rejects_missing_phone_number(string? input)
    {
        var result = normalizer.Normalize(input);

        Assert.False(result.IsValid);
        Assert.Null(result.CanonicalNumber);
        Assert.Equal(OwnerPhoneNormalizationErrorCodes.Missing, result.ErrorCode);
    }

    [Theory]
    [InlineData("912345678")]          // Missing 0, 84, or +84 prefix.
    [InlineData("0212345678")]         // Fixed-line leading digit.
    [InlineData("0412345678")]         // Unsupported mobile leading digit.
    [InlineData("0612345678")]         // Unsupported mobile leading digit.
    [InlineData("091234567")]          // Too short.
    [InlineData("09123456789")]        // Too long.
    [InlineData("+85912345678")]       // Wrong country code.
    [InlineData("+840912345678")]      // Country code combined with trunk prefix.
    public void Normalize_rejects_unsupported_numbers(string input)
    {
        var result = normalizer.Normalize(input);

        Assert.False(result.IsValid);
        Assert.Null(result.CanonicalNumber);
        Assert.Equal(OwnerPhoneNormalizationErrorCodes.UnsupportedNumber, result.ErrorCode);
    }

    [Theory]
    [InlineData("09123A5678")]         // Letters.
    [InlineData("0912345678 ext 2")]   // Extension.
    [InlineData("(0912)345678")]       // Parentheses.
    [InlineData("０９１２３４５６７８")] // Full-width Unicode digits.
    [InlineData("٠٩١٢٣٤٥٦٧٨")]         // Arabic-Indic Unicode digits.
    public void Normalize_rejects_invalid_characters(string input)
    {
        var result = normalizer.Normalize(input);

        Assert.False(result.IsValid);
        Assert.Null(result.CanonicalNumber);
        Assert.Equal(OwnerPhoneNormalizationErrorCodes.InvalidFormat, result.ErrorCode);
    }

    [Theory]
    [InlineData("-0912345678")]
    [InlineData(".0912345678")]
    [InlineData("0912345678-")]
    [InlineData("0912345678.")]
    [InlineData("0912--345678")]
    [InlineData("0912..345678")]
    [InlineData("0912  345678")]
    [InlineData("0912-.345678")]
    [InlineData("+ 84912345678")]
    [InlineData("+ 84 912 345 678")]
    public void Normalize_rejects_misplaced_or_repeated_separators(string input)
    {
        var result = normalizer.Normalize(input);

        Assert.False(result.IsValid);
        Assert.Null(result.CanonicalNumber);
        Assert.Equal(OwnerPhoneNormalizationErrorCodes.InvalidFormat, result.ErrorCode);
    }

    [Fact]
    public void Normalize_is_idempotent_for_canonical_number()
    {
        var first = normalizer.Normalize("0912-345-678");

        Assert.True(first.IsValid);
        var second = normalizer.Normalize(first.CanonicalNumber);

        Assert.True(second.IsValid);
        Assert.Equal(first.CanonicalNumber, second.CanonicalNumber);
        Assert.Null(second.ErrorCode);
    }
}
