using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Tests.Unit.Identity;

public class ActiveUserCookieEventsTests
{
    [Theory]
    [InlineData(false, SystemRoleNames.Admin)]
    [InlineData(true, null)]
    [InlineData(true, "UnknownRole")]
    public async Task InvalidCurrentAccountRejectsCookieAndSignsOutImmediately(
        bool isActive,
        string? roleName)
    {
        var authentication = new RecordingAuthenticationService();
        var context = CreateContext(authentication);
        var store = new StubPermissionStore(new UserPermissionState(
            isActive,
            roleName,
            new HashSet<string>(StringComparer.Ordinal)));
        var events = new ActiveUserCookieEvents(store, new PassingSecurityStampValidator());

        await events.ValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.Equal(IdentityConstants.ApplicationScheme, authentication.SignedOutScheme);
    }

    [Fact]
    public async Task ActiveAccountWithExactlyOneSystemRoleKeepsCookie()
    {
        var authentication = new RecordingAuthenticationService();
        var context = CreateContext(authentication);
        var store = new StubPermissionStore(new UserPermissionState(
            true,
            SystemRoleNames.Receptionist,
            new HashSet<string>(StringComparer.Ordinal)));
        var events = new ActiveUserCookieEvents(store, new PassingSecurityStampValidator());

        await events.ValidatePrincipal(context);

        Assert.NotNull(context.Principal);
        Assert.Null(authentication.SignedOutScheme);
    }

    [Fact]
    public async Task RejectedSecurityStampRejectsCookieBeforeActiveAccountCheck()
    {
        var authentication = new RecordingAuthenticationService();
        var context = CreateContext(authentication);
        var events = new ActiveUserCookieEvents(
            new StubPermissionStore(new UserPermissionState(
                true,
                SystemRoleNames.Admin,
                new HashSet<string>(StringComparer.Ordinal))),
            new RejectingSecurityStampValidator());

        await events.ValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.Null(authentication.SignedOutScheme);
    }

    private static CookieValidatePrincipalContext CreateContext(
        RecordingAuthenticationService authentication)
    {
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authentication)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1")],
            IdentityConstants.ApplicationScheme));
        var ticket = new AuthenticationTicket(principal, IdentityConstants.ApplicationScheme);
        var scheme = new AuthenticationScheme(
            IdentityConstants.ApplicationScheme,
            IdentityConstants.ApplicationScheme,
            typeof(CookieAuthenticationHandler));

        return new CookieValidatePrincipalContext(
            httpContext,
            scheme,
            new CookieAuthenticationOptions(),
            ticket);
    }

    private sealed class StubPermissionStore(UserPermissionState? state) : IUserPermissionStore
    {
        public Task<UserPermissionState?> FindByUserIdAsync(
            string userId,
            CancellationToken cancellationToken = default) => Task.FromResult(state);
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public string? SignedOutScheme { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            SignedOutScheme = scheme;
            return Task.CompletedTask;
        }
    }

    private sealed class PassingSecurityStampValidator : ISecurityStampValidator
    {
        public Task ValidateAsync(CookieValidatePrincipalContext context) => Task.CompletedTask;
    }

    private sealed class RejectingSecurityStampValidator : ISecurityStampValidator
    {
        public Task ValidateAsync(CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();
            return Task.CompletedTask;
        }
    }
}
