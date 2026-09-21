using Microsoft.AspNetCore.Identity;
using VeterinaryHospitalManagement.Web.Authorization;

namespace VeterinaryHospitalManagement.Web.Services.Identity;

public static class UserManagementRules
{
    public static string ToSafeIdentityErrorMessage(IEnumerable<IdentityError> errors, string fallback)
    {
        var messages = errors.Select(error => error.Code switch
        {
            "DuplicateEmail" or "DuplicateUserName" => "Email này đã được sử dụng.",
            "InvalidEmail" or "InvalidUserName" => "Email không hợp lệ.",
            "PasswordTooShort" => "Mật khẩu chưa đủ độ dài yêu cầu.",
            "PasswordRequiresDigit" => "Mật khẩu phải có ít nhất một chữ số.",
            "PasswordRequiresLower" => "Mật khẩu phải có ít nhất một chữ thường.",
            "PasswordRequiresUpper" => "Mật khẩu phải có ít nhất một chữ hoa.",
            "PasswordRequiresNonAlphanumeric" => "Mật khẩu phải có ít nhất một ký tự đặc biệt.",
            "PasswordRequiresUniqueChars" => "Mật khẩu cần thêm ký tự khác nhau.",
            _ => fallback
        }).Distinct(StringComparer.Ordinal).ToArray();

        return messages.Length == 0 ? fallback : string.Join(" ", messages);
    }

    public static bool IsSystemRole(string? roleName) =>
        !string.IsNullOrWhiteSpace(roleName) && SystemRoleNames.All.Contains(roleName);

    public static bool WouldRemoveLastActiveAdmin(
        string currentRole,
        bool currentIsActive,
        int activeAdminCount,
        string nextRole,
        bool nextIsActive) =>
        currentIsActive
        && string.Equals(currentRole, SystemRoleNames.Admin, StringComparison.Ordinal)
        && activeAdminCount <= 1
        && (!nextIsActive || !string.Equals(nextRole, SystemRoleNames.Admin, StringComparison.Ordinal));
}
