using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;

namespace VeterinaryHospitalManagement.Tests.Integration.Identity;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class CookieTransportIntegrationTests
{
    [IdentitySqlServerFact]
    public async Task Login_emits_an_application_cookie_that_requires_https_and_is_http_only()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        var loginPage = await client.GetStringAsync("/Account/Login");
        using var login = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Email", IdentitySqlServerTestEnvironment.BootstrapAdminEmail),
            new KeyValuePair<string, string>("Password", IdentitySqlServerTestEnvironment.BootstrapAdminPassword),
            new KeyValuePair<string, string>("RememberMe", "false"),
            new KeyValuePair<string, string>("__RequestVerificationToken", ExtractAntiForgeryToken(loginPage))
        ]);

        using var response = await client.PostAsync("/Account/Login", login);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var applicationCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
        Assert.Contains("; secure", applicationCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; httponly", applicationCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; samesite=lax", applicationCookie, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.CultureInvariant);
        return Assert.IsType<string>(match.Groups["token"].Value);
    }
}
