using VeterinaryHospitalManagement.Web.Data.Seed;

namespace VeterinaryHospitalManagement.Tests.Unit.Identity;

public class BootstrapAdminRulesTests
{
    [Fact]
    public void ExistingInactiveBootstrapAdminFailsExplicitly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BootstrapAdminRules.EnsureExistingAdminCanBootstrap(isActive: false, isAdmin: true));

        Assert.Contains("inactive", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExistingActiveAdminCanBootstrap()
    {
        BootstrapAdminRules.EnsureExistingAdminCanBootstrap(isActive: true, isAdmin: true);
    }
}
