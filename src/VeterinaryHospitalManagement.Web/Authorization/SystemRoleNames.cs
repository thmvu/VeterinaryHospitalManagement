namespace VeterinaryHospitalManagement.Web.Authorization;

public static class SystemRoleNames
{
    public const string Receptionist = "Receptionist";
    public const string Veterinarian = "Veterinarian";
    public const string Manager = "Manager";
    public const string Admin = "Admin";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        [Receptionist, Veterinarian, Manager, Admin],
        StringComparer.Ordinal);
}
