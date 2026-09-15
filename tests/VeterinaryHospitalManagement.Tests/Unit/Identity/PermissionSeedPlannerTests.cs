using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data.Seed;

namespace VeterinaryHospitalManagement.Tests.Unit.Identity;

public class PermissionSeedPlannerTests
{
    [Fact]
    public void PartialCatalogWithoutBaselineMarkerRepairsAllDefaultGrants()
    {
        var existingCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            PermissionCodes.OwnerView
        };

        var plan = PermissionSeedPlanner.Build(
            baselineMarkerExists: false,
            existingCodes,
            EmptyGrants());

        Assert.True(plan.AddBaselineMarker);
        Assert.DoesNotContain(PermissionCodes.OwnerView, plan.MissingPermissionCodes);
        Assert.Contains(PermissionCodes.OwnerView, plan.MissingGrants[SystemRoleNames.Receptionist]);
        Assert.Contains(PermissionCodes.OwnerView, plan.MissingGrants[SystemRoleNames.Veterinarian]);
        Assert.Contains(PermissionCodes.OwnerView, plan.MissingGrants[SystemRoleNames.Manager]);
        Assert.Contains(PermissionCodes.OwnerView, plan.MissingGrants[SystemRoleNames.Admin]);
    }

    [Fact]
    public void ExistingBaselineGrantsOnlyNewPermissionDefaultsAndPreservesRemovedOldGrant()
    {
        var existingCodes = PermissionCatalog.All
            .Select(definition => definition.Code)
            .Where(code => code != PermissionCodes.OwnerView)
            .ToHashSet(StringComparer.Ordinal);

        var plan = PermissionSeedPlanner.Build(
            baselineMarkerExists: true,
            existingCodes,
            EmptyGrants());

        Assert.False(plan.AddBaselineMarker);
        Assert.Equal([PermissionCodes.OwnerView], plan.MissingPermissionCodes);
        Assert.Contains(PermissionCodes.OwnerView, plan.MissingGrants[SystemRoleNames.Receptionist]);
        Assert.DoesNotContain(PermissionCodes.OwnerManage, plan.MissingGrants[SystemRoleNames.Receptionist]);
    }

    [Fact]
    public void RerunAfterBaselineDoesNotRestoreRemovedGrant()
    {
        var existingCodes = PermissionCatalog.All
            .Select(definition => definition.Code)
            .ToHashSet(StringComparer.Ordinal);

        var plan = PermissionSeedPlanner.Build(
            baselineMarkerExists: true,
            existingCodes,
            EmptyGrants());

        Assert.Empty(plan.MissingPermissionCodes);
        Assert.All(plan.MissingGrants.Values, Assert.Empty);
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> EmptyGrants() =>
        SystemRoleNames.All.ToDictionary(
            role => role,
            _ => (IReadOnlySet<string>)new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
}
