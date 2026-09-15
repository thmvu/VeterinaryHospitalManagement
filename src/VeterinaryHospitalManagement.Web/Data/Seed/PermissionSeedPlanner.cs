using VeterinaryHospitalManagement.Web.Authorization;

namespace VeterinaryHospitalManagement.Web.Data.Seed;

public sealed record PermissionSeedPlan(
    IReadOnlyList<string> MissingPermissionCodes,
    IReadOnlyDictionary<string, IReadOnlySet<string>> MissingGrants,
    bool AddBaselineMarker);

public static class PermissionSeedPlanner
{
    public static PermissionSeedPlan Build(
        bool baselineMarkerExists,
        IReadOnlySet<string> existingPermissionCodes,
        IReadOnlyDictionary<string, IReadOnlySet<string>> existingGrants)
    {
        var missingCodes = PermissionCatalog.All
            .Select(definition => definition.Code)
            .Where(code => !existingPermissionCodes.Contains(code))
            .ToArray();
        var codesToReconcile = (baselineMarkerExists
                ? missingCodes
                : PermissionCatalog.All.Select(definition => definition.Code))
            .ToHashSet(StringComparer.Ordinal);
        var missingGrants = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        foreach (var roleName in SystemRoleNames.All)
        {
            existingGrants.TryGetValue(roleName, out var currentGrants);
            currentGrants ??= new HashSet<string>(StringComparer.Ordinal);

            missingGrants[roleName] = PermissionCatalog.DefaultPermissionsFor(roleName)
                .Where(codesToReconcile.Contains)
                .Where(code => !currentGrants.Contains(code))
                .ToHashSet(StringComparer.Ordinal);
        }

        return new PermissionSeedPlan(
            missingCodes,
            missingGrants,
            AddBaselineMarker: !baselineMarkerExists);
    }
}
