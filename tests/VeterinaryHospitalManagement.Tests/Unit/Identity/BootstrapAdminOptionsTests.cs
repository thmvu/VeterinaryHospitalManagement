using Microsoft.AspNetCore.Identity;
using VeterinaryHospitalManagement.Web.Configuration;

namespace VeterinaryHospitalManagement.Tests.Unit.Identity;

public class BootstrapAdminOptionsTests
{
    private static readonly IdentityOptions IdentityOptions = new();

    [Fact]
    public void DisabledBootstrapDoesNotRequireCredentials()
    {
        var result = Validate(new BootstrapAdminOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void EnabledBootstrapAcceptsValidCredentials()
    {
        var options = ValidOptions();

        var result = Validate(options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(nameof(BootstrapAdminOptions.Email), "BootstrapAdmin:Email")]
    [InlineData(nameof(BootstrapAdminOptions.Password), "BootstrapAdmin:Password")]
    [InlineData(nameof(BootstrapAdminOptions.FullName), "BootstrapAdmin:FullName")]
    public void EnabledBootstrapNamesMissingConfigurationKeyWithoutDisclosingOtherValues(
        string propertyName,
        string expectedKey)
    {
        var options = ValidOptions();
        typeof(BootstrapAdminOptions).GetProperty(propertyName)!.SetValue(options, " ");

        var result = Validate(options);
        var failures = string.Join(" ", result.Failures ?? []);

        Assert.True(result.Failed);
        Assert.Contains(expectedKey, failures);
        Assert.DoesNotContain("admin@example.test", failures);
        Assert.DoesNotContain("Valid1!Password", failures);
        Assert.DoesNotContain("Quản trị hệ thống", failures);
    }

    [Fact]
    public void EnabledBootstrapRejectsInvalidEmailWithoutDisclosingIt()
    {
        var options = ValidOptions();
        options.Email = "not-an-email-secret";

        var result = Validate(options);
        var failures = string.Join(" ", result.Failures ?? []);

        Assert.True(result.Failed);
        Assert.Contains("BootstrapAdmin:Email", failures);
        Assert.DoesNotContain(options.Email, failures);
    }

    [Fact]
    public void EnabledBootstrapRejectsPasswordThatDoesNotMeetIdentityPolicyWithoutDisclosingIt()
    {
        var options = ValidOptions();
        options.Password = "weak";

        var result = Validate(options);
        var failures = string.Join(" ", result.Failures ?? []);

        Assert.True(result.Failed);
        Assert.Contains("BootstrapAdmin:Password", failures);
        Assert.DoesNotContain(options.Password, failures);
    }

    [Fact]
    public void EnabledBootstrapRejectsFullNameLongerThanDatabaseLimitWithoutDisclosingIt()
    {
        var options = ValidOptions();
        options.FullName = new string('X', 151);

        var result = Validate(options);
        var failures = string.Join(" ", result.Failures ?? []);

        Assert.True(result.Failed);
        Assert.Contains("BootstrapAdmin:FullName", failures);
        Assert.DoesNotContain(options.FullName, failures);
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(BootstrapAdminOptions options) =>
        new BootstrapAdminOptionsValidator(IdentityOptions).Validate(null, options);

    private static BootstrapAdminOptions ValidOptions() => new()
    {
        Enabled = true,
        Email = "admin@example.test",
        Password = "Valid1!Password",
        FullName = "Quản trị hệ thống"
    };
}
