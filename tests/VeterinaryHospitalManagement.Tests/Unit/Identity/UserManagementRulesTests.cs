using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Identity;
using Microsoft.AspNetCore.Identity;

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

    [Fact]
    public void Converts_password_policy_errors_to_safe_vietnamese_guidance()
    {
        var errors = new[]
        {
            new IdentityError { Code = "PasswordTooShort", Description = "Passwords must be at least 8 characters." },
            new IdentityError { Code = "PasswordRequiresDigit", Description = "Passwords must have at least one digit ('0'-'9')." }
        };

        var message = UserManagementRules.ToSafeIdentityErrorMessage(errors, "Không thể tạo tài khoản.");

        Assert.Contains("Mật khẩu chưa đủ độ dài yêu cầu.", message);
        Assert.Contains("Mật khẩu phải có ít nhất một chữ số.", message);
        Assert.DoesNotContain("Passwords", message);
    }

    [Fact]
    public void Converts_duplicate_email_error_without_exposing_identity_internals()
    {
        var errors = new[]
        {
            new IdentityError { Code = "DuplicateUserName", Description = "User name 'doctor@example.test' is already taken." }
        };

        var message = UserManagementRules.ToSafeIdentityErrorMessage(errors, "Không thể tạo tài khoản.");

        Assert.Equal("Email này đã được sử dụng.", message);
        Assert.DoesNotContain("User name", message);
    }
}
