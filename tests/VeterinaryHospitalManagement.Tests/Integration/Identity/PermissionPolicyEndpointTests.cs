using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VeterinaryHospitalManagement.Web.Authorization;

namespace VeterinaryHospitalManagement.Tests.Integration.Identity;

public sealed class PermissionPolicyEndpointTests
{
    [Fact]
    public async Task Authenticated_request_with_an_unknown_permission_returns_forbidden_instead_of_500()
    {
        using var factory = new FoundationWebApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                        options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.SchemeName,
                        _ => { });
                services
                    .AddControllersWithViews()
                    .AddApplicationPart(typeof(UnknownPermissionTestController).Assembly);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using HttpResponseMessage response = await client.GetAsync("/__tests/unknown-permission");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "PermissionPolicyTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "permission-policy-test-user")],
                SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

[ApiController]
[Route("__tests/unknown-permission")]
public sealed class UnknownPermissionTestController : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("Typo.DoesNotExist")]
    public IActionResult Get() => Ok();
}
