using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Tests.Unit.Identity;

public class UserManagementRulesTests
{
    [Theory]
    [InlineData(SystemRoleNames.Admin, true)]
    [InlineData(SystemRoleNames.Receptionist, true)]
    [InlineData("Other", false)]
    [InlineData("", false)]
    public void RecognizesOnlySystemRoles(string roleName, bool expected)
    {
        Assert.Equal(expected, UserManagementRules.IsSystemRole(roleName));
    }

    [Fact]
    public void PreventsRemovingTheLastActiveAdmin()
    {
        Assert.True(UserManagementRules.WouldRemoveLastActiveAdmin(
            currentRole: SystemRoleNames.Admin,
            currentIsActive: true,
            activeAdminCount: 1,
            nextRole: SystemRoleNames.Receptionist,
            nextIsActive: true));
    }

    [Fact]
    public void AllowsChangingRoleWhenAnotherActiveAdminExists()
    {
        Assert.False(UserManagementRules.WouldRemoveLastActiveAdmin(
            currentRole: SystemRoleNames.Admin,
            currentIsActive: true,
            activeAdminCount: 2,
            nextRole: SystemRoleNames.Manager,
            nextIsActive: true));
    }
}
