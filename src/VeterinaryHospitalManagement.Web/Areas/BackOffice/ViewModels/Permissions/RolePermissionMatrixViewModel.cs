namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Permissions;

public sealed class RolePermissionMatrixViewModel
{
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<PermissionGroupViewModel> Groups { get; init; } = [];
    public IReadOnlyDictionary<string, IReadOnlySet<string>> GrantsByRole { get; init; } =
        new Dictionary<string, IReadOnlySet<string>>();
    public IReadOnlySet<string> AdminOnlyPermissions { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string> Dependencies { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record PermissionGroupViewModel(
    string GroupName,
    IReadOnlyList<PermissionItemViewModel> Permissions
);

public sealed record PermissionItemViewModel(
    string Code,
    string Name,
    string? RequiredCode
);
