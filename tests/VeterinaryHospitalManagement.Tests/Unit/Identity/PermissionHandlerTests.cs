using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Tests.Unit.Identity;

public sealed class PermissionHandlerTests
{
    [Fact]
    public async Task Handler_succeeds_when_the_current_user_has_the_required_permission()
    {
        var requirement = new PermissionRequirement(PermissionCodes.OwnerView);
        var context = new AuthorizationHandlerContext(
            [requirement],
            AuthenticatedUser(),
            resource: null);
        var handler = new PermissionAuthorizationHandler(new StubPermissionService(true));

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Handler_does_not_succeed_when_the_current_user_lacks_the_required_permission()
    {
        var requirement = new PermissionRequirement(PermissionCodes.OwnerManage);
        var context = new AuthorizationHandlerContext(
            [requirement],
            AuthenticatedUser(),
            resource: null);
        var handler = new PermissionAuthorizationHandler(new StubPermissionService(false));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Policy_provider_builds_a_policy_for_a_known_permission()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        AuthorizationPolicy? policy = await provider.GetPolicyAsync(
            PermissionPolicyName.For(PermissionCodes.InvoicePrint));

        var requirement = Assert.Single(policy!.Requirements.OfType<PermissionRequirement>());
        Assert.Equal(PermissionCodes.InvoicePrint, requirement.PermissionCode);
    }

    [Fact]
    public async Task Policy_provider_builds_a_deny_policy_for_an_unknown_permission()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        AuthorizationPolicy? policy = await provider.GetPolicyAsync(
            PermissionPolicyName.For("Typo.DoesNotExist"));

        Assert.NotNull(policy);
        Assert.Single(policy.Requirements.OfType<DenyPermissionRequirement>());
        Assert.Empty(policy.Requirements.OfType<PermissionRequirement>());
    }

    private static ClaimsPrincipal AuthenticatedUser() =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1")],
            authenticationType: "Test"));

    private sealed class StubPermissionService(bool allowed) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string permissionCode,
            CancellationToken cancellationToken = default) => Task.FromResult(allowed);
    }
}
