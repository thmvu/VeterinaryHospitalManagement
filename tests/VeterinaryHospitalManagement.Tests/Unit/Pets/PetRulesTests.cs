using VeterinaryHospitalManagement.Web.Services.Pets;

namespace VeterinaryHospitalManagement.Tests.Unit.Pets;

public sealed class PetRulesTests
{
    [Theory]
    [InlineData(1L, "PET-000001")]
    [InlineData(8L, "PET-000008")]
    [InlineData(999999L, "PET-999999")]
    public void FormatPetCode_formats_sequential_values(long sequenceValue, string expected)
    {
        Assert.Equal(expected, PetRules.FormatPetCode(sequenceValue));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1000000L)]
    public void FormatPetCode_rejects_out_of_range_values(long sequenceValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PetRules.FormatPetCode(sequenceValue));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeName_rejects_missing_or_whitespace(string? input)
    {
        var ex = Assert.Throws<PetManagementException>(() => PetRules.NormalizeName(input));
        Assert.Equal("Tên thú cưng là bắt buộc.", ex.Message);
    }

    [Fact]
    public void NormalizeName_trims_and_rejects_exceeding_max_length()
    {
        Assert.Equal("Milo", PetRules.NormalizeName("  Milo  "));
        var tooLong = new string('A', 101);
        var ex = Assert.Throws<PetManagementException>(() => PetRules.NormalizeName(tooLong));
        Assert.Contains("100 ký tự", ex.Message);
    }

    [Fact]
    public void NormalizeOptional_handles_null_whitespace_and_length()
    {
        Assert.Null(PetRules.NormalizeOptional(null, 100, "Màu lông"));
        Assert.Null(PetRules.NormalizeOptional("   ", 100, "Màu lông"));
        Assert.Equal("Vàng trắng", PetRules.NormalizeOptional("  Vàng trắng  ", 100, "Màu lông"));

        var tooLong = new string('x', 101);
        var ex = Assert.Throws<PetManagementException>(() => PetRules.NormalizeOptional(tooLong, 100, "Màu lông"));
        Assert.Contains("Màu lông không được vượt quá 100 ký tự", ex.Message);
    }

    [Fact]
    public void ValidateBirthDate_rejects_future_dates_relative_to_today()
    {
        var today = new DateOnly(2026, 9, 17);
        // Null or past/today is allowed
        PetRules.ValidateBirthDate(null, today);
        PetRules.ValidateBirthDate(today, today);
        PetRules.ValidateBirthDate(today.AddDays(-10), today);

        // Future date is rejected
        var ex = Assert.Throws<PetManagementException>(() => PetRules.ValidateBirthDate(today.AddDays(1), today));
        Assert.Contains("không được vượt quá ngày hiện tại", ex.Message);
    }
}
