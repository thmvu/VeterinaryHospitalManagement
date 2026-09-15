namespace VeterinaryHospitalManagement.Web.Data.Seed;

public static class BootstrapAdminRules
{
    public static void EnsureExistingAdminCanBootstrap(bool isActive, bool isAdmin)
    {
        if (!isAdmin)
        {
            throw new InvalidOperationException(
                "BootstrapAdmin:Email already belongs to a user who is not an Admin.");
        }

        if (!isActive)
        {
            throw new InvalidOperationException(
                "BootstrapAdmin: existing Admin account is inactive and must be reactivated deliberately.");
        }
    }
}
