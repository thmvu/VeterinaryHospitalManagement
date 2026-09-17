using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Tests.Integration.OwnersPets;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class PetsEndpointAuthorizationTests
{
    [IdentitySqlServerFact]
    public async Task Anonymous_request_to_pets_details_is_challenged_to_login()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/BackOffice/Pets/Details/1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [IdentitySqlServerFact]
    public async Task Veterinarian_with_pet_view_cannot_access_pet_create()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var adminId = await StartAndFindBootstrapAdminAsync(factory);
        await CreateUserAsync(factory, adminId, "pets.vet@example.test", SystemRoleNames.Veterinarian, "Integration.PetsVet1!");

        using var client = CreateClient(factory);
        await LoginAsync(client, "pets.vet@example.test", "Integration.PetsVet1!");

        // Veterinarian has PetView, but NOT PetManage -> GET /BackOffice/Pets/Create must be 403
        using var createResponse = await client.GetAsync("/BackOffice/Pets/Create?ownerId=1");
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

        // POST /BackOffice/Pets/Create must also be 403
        using var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Name", "Pet Unauthorized"),
            new KeyValuePair<string, string>("OwnerId", "1"),
            new KeyValuePair<string, string>("SpeciesId", "1"),
            new KeyValuePair<string, string>("Sex", "Male")
        ]);
        using var postResponse = await client.PostAsync("/BackOffice/Pets/Create", form);
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);
    }

    [IdentitySqlServerFact]
    public async Task Receptionist_with_pet_manage_creates_pet_via_post()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var adminId = await StartAndFindBootstrapAdminAsync(factory);

        int ownerId;
        int speciesId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Web.Data.ApplicationDbContext>();
            var owner = new Owner
            {
                OwnerCode = "OWN-000001",
                FullName = "Chủ Test",
                PhoneNumber = "+84912345678",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var species = new Species { Code = "DOG", Name = "Chó", IsActive = true };
            db.Owners.Add(owner);
            db.Species.Add(species);
            await db.SaveChangesAsync();
            ownerId = owner.Id;
            speciesId = species.Id;
        }

        await CreateUserAsync(factory, adminId, "pets.receptionist@example.test", SystemRoleNames.Receptionist, "Integration.PetsRec1!");
        using var client = CreateClient(factory);
        await LoginAsync(client, "pets.receptionist@example.test", "Integration.PetsRec1!");

        var createPage = await client.GetStringAsync($"/BackOffice/Pets/Create?ownerId={ownerId}");
        using var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("OwnerId", ownerId.ToString()),
            new KeyValuePair<string, string>("Name", "KiKi"),
            new KeyValuePair<string, string>("SpeciesId", speciesId.ToString()),
            new KeyValuePair<string, string>("Sex", "Male"),
            new KeyValuePair<string, string>("__RequestVerificationToken", ExtractAntiForgeryToken(createPage))
        ]);

        using var response = await client.PostAsync("/BackOffice/Pets/Create", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Web.Data.ApplicationDbContext>();
            var pet = Assert.Single(db.Pets.ToList());
            Assert.Equal("KiKi", pet.Name);
            Assert.Equal("PET-000001", pet.PetCode);
        }
    }

    private static HttpClient CreateClient(IdentitySqlServerWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task<string> StartAndFindBootstrapAdminAsync(IdentitySqlServerWebApplicationFactory factory)
    {
        using var client = CreateClient(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Web.Data.ApplicationDbContext>();
        return await db.Users
            .Where(user => user.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant())
            .Select(user => user.Id)
            .SingleAsync();
    }

    private static async Task CreateUserAsync(
        IdentitySqlServerWebApplicationFactory factory,
        string adminId,
        string email,
        string roleName,
        string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUserManagementService>().CreateAsync(
            new CreateManagedUserRequest(adminId, "Pets Endpoint Test", email, password, roleName));
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var loginPage = await client.GetStringAsync("/Account/Login");
        using var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Email", email),
            new KeyValuePair<string, string>("Password", password),
            new KeyValuePair<string, string>("RememberMe", "false"),
            new KeyValuePair<string, string>("__RequestVerificationToken", ExtractAntiForgeryToken(loginPage))
        ]);
        using var response = await client.PostAsync("/Account/Login", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
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
