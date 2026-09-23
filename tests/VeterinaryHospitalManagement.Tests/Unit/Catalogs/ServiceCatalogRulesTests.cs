using VeterinaryHospitalManagement.Web.Services.Catalogs;

namespace VeterinaryHospitalManagement.Tests.Unit.Catalogs;

public sealed class ServiceCatalogRulesTests
{
    [Theory]
    [InlineData(" dv-kham ", "DV-KHAM")]
    [InlineData("Tiêm Chủng", "TIÊM CHỦNG")]
    public void NormalizeCode_trims_and_uppercases(string input, string expected) =>
        Assert.Equal(expected, ServiceCatalogRules.NormalizeCode(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeCode_rejects_missing_values(string? input) =>
        Assert.Throws<ServiceCatalogManagementException>(() => ServiceCatalogRules.NormalizeCode(input));

    [Fact]
    public void NormalizeCode_rejects_values_over_30_characters() =>
        Assert.Throws<ServiceCatalogManagementException>(() => ServiceCatalogRules.NormalizeCode(new string('A', 31)));

    [Fact]
    public void NormalizePrice_accepts_two_decimals_and_rejects_negative_or_excess_scale()
    {
        Assert.Equal(125000.50m, ServiceCatalogRules.NormalizePrice(125000.50m));
        Assert.Throws<ServiceCatalogManagementException>(() => ServiceCatalogRules.NormalizePrice(-1m));
        Assert.Throws<ServiceCatalogManagementException>(() => ServiceCatalogRules.NormalizePrice(1.001m));
    }

    [Fact]
    public void Text_fields_are_trimmed_validated_and_optional_description_becomes_null()
    {
        Assert.Equal("Khám tổng quát", ServiceCatalogRules.NormalizeName(" Khám tổng quát "));
        Assert.Equal("Khám", ServiceCatalogRules.NormalizeCategory(" Khám "));
        Assert.Null(ServiceCatalogRules.NormalizeDescription("  "));
        Assert.Throws<ServiceCatalogManagementException>(() => ServiceCatalogRules.NormalizeName(new string('A', 151)));
        Assert.Throws<ServiceCatalogManagementException>(() => ServiceCatalogRules.NormalizeCategory(new string('A', 101)));
        Assert.Throws<ServiceCatalogManagementException>(() => ServiceCatalogRules.NormalizeDescription(new string('A', 1001)));
    }
}
