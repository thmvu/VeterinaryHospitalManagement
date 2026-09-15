using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace VeterinaryHospitalManagement.Tests.Integration.Identity;

public class AccountEndpointTests : IClassFixture<FoundationWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AccountEndpointTests(FoundationWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task LoginPageIsPubliclyAvailable()
    {
        var response = await _client.GetAsync("/Account/Login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Đăng nhập", html);
    }

    [Fact]
    public async Task RegistrationEndpointDoesNotExist()
    {
        var response = await _client.GetAsync("/Account/Register");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousUserIsRedirectedFromChangePasswordToLoginWithLocalReturnUrl()
    {
        var response = await _client.GetAsync("/Account/ChangePassword");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "https://localhost/Account/Login?ReturnUrl=%2FAccount%2FChangePassword",
            response.Headers.Location?.AbsoluteUri);
    }

    [Fact]
    public void EnabledBootstrapWithInvalidPasswordFailsAtStartupWithoutDisclosingConfiguredValues()
    {
        const string email = "private-admin@example.test";
        const string password = "private-weak-password";
        const string fullName = "Private Administrator Name";
        using var factory = new FoundationWebApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BootstrapAdmin:Enabled"] = "true",
                    ["BootstrapAdmin:Email"] = email,
                    ["BootstrapAdmin:Password"] = password,
                    ["BootstrapAdmin:FullName"] = fullName
                })));

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        var failure = exception.ToString();

        Assert.Contains("BootstrapAdmin:Password", failure);
        Assert.DoesNotContain(email, failure);
        Assert.DoesNotContain(password, failure);
        Assert.DoesNotContain(fullName, failure);
    }
}
