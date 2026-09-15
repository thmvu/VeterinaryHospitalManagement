using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace VeterinaryHospitalManagement.Tests.Infrastructure.Identity;

internal sealed class IdentitySqlServerWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = IdentitySqlServerTestEnvironment.ConnectionString,
                ["IdentitySeed:RunOnStartup"] = "true",
                ["BootstrapAdmin:Enabled"] = "true",
                ["BootstrapAdmin:Email"] = IdentitySqlServerTestEnvironment.BootstrapAdminEmail,
                ["BootstrapAdmin:Password"] = IdentitySqlServerTestEnvironment.BootstrapAdminPassword,
                ["BootstrapAdmin:FullName"] = IdentitySqlServerTestEnvironment.BootstrapAdminFullName
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDataProtectionProvider>();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.PostConfigure<KeyManagementOptions>(options =>
                options.XmlRepository = new InMemoryXmlRepository());
        });
    }

    private sealed class InMemoryXmlRepository : IXmlRepository
    {
        private readonly List<XElement> elements = [];

        public IReadOnlyCollection<XElement> GetAllElements() =>
            elements.Select(element => new XElement(element)).ToArray();

        public void StoreElement(XElement element, string friendlyName) =>
            elements.Add(new XElement(element));
    }
}
