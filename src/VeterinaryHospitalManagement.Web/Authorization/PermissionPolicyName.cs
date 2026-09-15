namespace VeterinaryHospitalManagement.Web.Authorization;

public static class PermissionPolicyName
{
    public const string Prefix = "Permission:";

    public static string For(string permissionCode) => $"{Prefix}{permissionCode}";

    public static bool TryParse(string policyName, out string permissionCode)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            permissionCode = policyName[Prefix.Length..];
            return permissionCode.Length > 0;
        }

        permissionCode = string.Empty;
        return false;
    }
}
