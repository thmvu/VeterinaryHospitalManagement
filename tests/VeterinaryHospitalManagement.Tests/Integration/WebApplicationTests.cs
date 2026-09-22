using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Tests.Integration;

public class WebApplicationTests : IClassFixture<FoundationWebApplicationFactory>
{
    private readonly FoundationWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public WebApplicationTests(FoundationWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task Home_page_returns_the_Vietnamese_landing_page()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<html lang=\"vi\">", html);
        Assert.Contains("Veterinary Hospital Management", html);
        Assert.Contains("Quản lý bệnh viện thú y ngoại trú", html);
        Assert.Contains("<meta name=\"theme-color\" content=\"#2D3A31\"", html);
        Assert.DoesNotContain("Miu Miu", html);
        Assert.DoesNotContain("100%", html);
        Assert.Contains("botanical-hero", html);
    }

    [Fact]
    public async Task Anonymous_home_page_links_to_login()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("href=\"/Account/Login\"", html);
        Assert.Contains("Đăng nhập", html);
    }

    [Theory]
    [InlineData("/Home/Privacy")]
    [InlineData("/Account/Login")]
    public async Task Non_home_public_pages_render_inside_the_page_shell(string route)
    {
        var response = await _client.GetAsync(route);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"main-content\"", html);
        Assert.Contains("class=\"container-xxl page-shell\"", html);
    }

    [Fact]
    public async Task Authenticated_user_with_user_manage_permission_sees_user_management_link()
    {
        var authenticationState = new LandingAuthenticationState();
        using var factory = CreateAuthenticatedFactory(authenticationState, PermissionCodes.UserManage);
        using var client = CreateClient(factory);

        var html = await client.GetStringAsync("/");

        Assert.Contains("href=\"/BackOffice/Users\"", html);
        Assert.Contains("Quản lý tài khoản", html);
    }

    [Fact]
    public async Task User_create_page_renders_inside_the_authenticated_backoffice_shell()
    {
        var authenticationState = new LandingAuthenticationState();
        using var factory = CreateAuthenticatedFactory(authenticationState, PermissionCodes.UserManage);
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/BackOffice/Users/Create");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("class=\"app-shell\"", html);
        Assert.Contains("id=\"main-content\"", html);
        Assert.Contains("href=\"/css/site", html);
        Assert.Contains("data-ui=\"botanical-management-form\"", html);
    }

    [Fact]
    public async Task Authenticated_user_without_user_manage_permission_does_not_see_user_management_link()
    {
        var authenticationState = new LandingAuthenticationState();
        using var factory = CreateAuthenticatedFactory(authenticationState);
        using var client = CreateClient(factory);

        var html = await client.GetStringAsync("/");

        Assert.DoesNotContain("href=\"/BackOffice/Users\"", html);
        Assert.DoesNotContain("Quản lý tài khoản", html);
    }

    [Fact]
    public async Task Authenticated_navigation_shows_change_password_and_post_logout_with_antiforgery()
    {
        var authenticationState = new LandingAuthenticationState();
        using var factory = CreateAuthenticatedFactory(authenticationState);
        using var client = CreateClient(factory);

        var html = await client.GetStringAsync("/");

        Assert.Contains("href=\"/Account/ChangePassword\"", html);
        Assert.Contains("action=\"/Account/Logout\"", html);
        Assert.Contains("method=\"post\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("type=\"hidden\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Đăng xuất", html);
        Assert.DoesNotContain("href=\"/Account/Login\"", html);
    }

    [Fact]
    public async Task Logout_without_antiforgery_token_returns_bad_request_without_signing_out()
    {
        var authenticationState = new LandingAuthenticationState();
        using var factory = CreateAuthenticatedFactory(authenticationState);
        using var client = CreateClient(factory);

        using var response = await client.PostAsync(
            "/Account/Logout",
            new FormUrlEncodedContent([]));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(authenticationState.IsSignedIn);
    }

    [Fact]
    public async Task Logout_with_antiforgery_token_signs_out_and_protected_page_challenges()
    {
        var authenticationState = new LandingAuthenticationState();
        using var factory = CreateAuthenticatedFactory(authenticationState);
        using var client = CreateClient(factory);

        var homeHtml = await client.GetStringAsync("/");
        var tokenMatch = Regex.Match(
            homeHtml,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"[^>]*>",
            RegexOptions.CultureInvariant);
        Assert.True(tokenMatch.Success, "Expected the logout form to contain an antiforgery token.");

        using var logoutResponse = await client.PostAsync(
            "/Account/Logout",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = tokenMatch.Groups[1].Value
                }));

        Assert.Equal(System.Net.HttpStatusCode.Redirect, logoutResponse.StatusCode);
        Assert.Equal("/Account/Login", logoutResponse.Headers.Location?.OriginalString);
        Assert.False(authenticationState.IsSignedIn);

        using var protectedResponse = await client.GetAsync("/Account/ChangePassword");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, protectedResponse.StatusCode);
        Assert.StartsWith(
            "/Account/Login?ReturnUrl=",
            protectedResponse.Headers.Location?.OriginalString,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Home/Privacy")]
    [InlineData("/Account/Login")]
    public async Task Public_navigation_routes_return_ok(string route)
    {
        var response = await _client.GetAsync(route);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Landing_page_has_no_placeholder_or_unimplemented_module_links()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("href=\"#\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/Owners", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/BackOffice/Owners", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/Appointments", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/Visits", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/Invoices", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/Reports", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AccessDeniedPageReturnsForbiddenStatus()
    {
        var response = await _client.GetAsync("/Home/AccessDenied");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateAuthenticatedFactory(
        LandingAuthenticationState authenticationState,
        params string[] grantedPermissions) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuthenticationService>();
                services.AddSingleton<IAuthenticationService>(
                    new LandingTestAuthenticationService(authenticationState));
                services.RemoveAll<IPermissionService>();
                services.AddSingleton<IPermissionService>(
                    new StubPermissionService(grantedPermissions));
            }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

    private sealed class StubPermissionService(IEnumerable<string> grantedPermissions) : IPermissionService
    {
        private readonly HashSet<string> _grantedPermissions =
            new(grantedPermissions, StringComparer.Ordinal);

        public Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string permissionCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_grantedPermissions.Contains(permissionCode));
    }

    private sealed class LandingAuthenticationState
    {
        public bool IsSignedIn { get; set; } = true;
    }

    private sealed class LandingTestAuthenticationService(LandingAuthenticationState state)
        : IAuthenticationService
    {
        private const string SchemeName = "LandingTest";

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            if (!state.IsSignedIn)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "landing-test-user"),
                    new Claim(ClaimTypes.Name, "Landing Test User")
                ],
                SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
            context.Response.Redirect($"/Account/Login?ReturnUrl={returnUrl}");
            return Task.CompletedTask;
        }

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
        {
            state.IsSignedIn = true;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            state.IsSignedIn = false;
            return Task.CompletedTask;
        }
    }
}

public sealed class FoundationWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging =>
            logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.Error));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDataProtectionProvider>();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.PostConfigure<KeyManagementOptions>(options =>
            {
                options.XmlRepository = new InMemoryXmlRepository();
            });
        });
    }

    private sealed class InMemoryXmlRepository : IXmlRepository
    {
        private readonly List<XElement> _elements = [];

        public IReadOnlyCollection<XElement> GetAllElements() =>
            _elements.Select(element => new XElement(element)).ToArray();

        public void StoreElement(XElement element, string friendlyName) =>
            _elements.Add(new XElement(element));
    }
}
