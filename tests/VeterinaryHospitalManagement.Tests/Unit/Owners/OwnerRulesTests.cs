using VeterinaryHospitalManagement.Web.Services.Owners;

namespace VeterinaryHospitalManagement.Tests.Unit.Owners;

public sealed class OwnerRulesTests
{
    [Theory]
    [InlineData(1L, "OWN-000001")]
    [InlineData(9L, "OWN-000009")]
    [InlineData(12345L, "OWN-012345")]
    [InlineData(999999L, "OWN-999999")]
    public void FormatOwnerCode_pads_sequence_values_to_six_digits(long sequenceValue, string expected)
    {
        Assert.Equal(expected, OwnerRules.FormatOwnerCode(sequenceValue));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1000000L)]
    public void FormatOwnerCode_rejects_values_outside_the_sequence_range(long sequenceValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OwnerRules.FormatOwnerCode(sequenceValue));
    }

    [Fact]
    public void DescribePhoneError_maps_every_known_error_code_to_a_distinct_message()
    {
        var knownCodes = new[]
        {
            OwnerPhoneNormalizationErrorCodes.Missing,
            OwnerPhoneNormalizationErrorCodes.InvalidFormat,
            OwnerPhoneNormalizationErrorCodes.UnsupportedNumber
        };

        var messages = knownCodes.Select(OwnerRules.DescribePhoneError).ToList();

        Assert.All(messages, message => Assert.False(string.IsNullOrWhiteSpace(message)));
        Assert.Equal(messages.Count, messages.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("Số điện thoại không hợp lệ.", OwnerRules.DescribePhoneError("Phone.UnknownCode"));
    }
}
