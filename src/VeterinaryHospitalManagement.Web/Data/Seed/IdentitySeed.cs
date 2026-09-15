using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Configuration;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Seed;

public sealed class IdentitySeed(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<BootstrapAdminOptions> bootstrapOptions,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyDictionary<string, IdentityRole>> EnsureRolesAsync()
    {
        var roles = new Dictionary<string, IdentityRole>(StringComparer.Ordinal);

        foreach (var roleName in SystemRoleNames.All)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                role = new IdentityRole(roleName);
                var result = await roleManager.CreateAsync(role);
                EnsureSucceeded(result, $"Could not create system role '{roleName}'.");
            }

            roles.Add(roleName, role);
        }

        return roles;
    }

    public async Task EnsureBootstrapAdminAsync()
    {
        var options = bootstrapOptions.Value;
        if (!options.Enabled)
        {
            return;
        }

        var email = options.Email.Trim();
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            BootstrapAdminRules.EnsureExistingAdminCanBootstrap(
                existingUser.IsActive,
                await userManager.IsInRoleAsync(existingUser, SystemRoleNames.Admin));
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = options.FullName.Trim(),
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow()
        };

        var createResult = await userManager.CreateAsync(user, options.Password);
        EnsureSucceeded(createResult, "Could not create the bootstrap Admin user.");

        var addRoleResult = await userManager.AddToRoleAsync(user, SystemRoleNames.Admin);
        if (!addRoleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            EnsureSucceeded(addRoleResult, "Could not assign the Admin role to the bootstrap user.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var codes = string.Join(", ", result.Errors.Select(error => error.Code));
        throw new InvalidOperationException($"{message} Identity error codes: {codes}");
    }
}
