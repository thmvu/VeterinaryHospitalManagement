using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Xml.Linq;

namespace VeterinaryHospitalManagement.Tests.Integration;

public class WebApplicationTests : IClassFixture<FoundationWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WebApplicationTests(FoundationWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task HomePageReturnsVietnameseFoundationPage()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<html lang=\"vi\">", html);
        Assert.Contains("Veterinary Hospital Management", html);
    }

    [Fact]
    public async Task AccessDeniedPageReturnsForbiddenStatus()
    {
        var response = await _client.GetAsync("/Home/AccessDenied");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
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
