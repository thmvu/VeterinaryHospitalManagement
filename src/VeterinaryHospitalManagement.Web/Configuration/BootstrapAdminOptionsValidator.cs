using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace VeterinaryHospitalManagement.Web.Configuration;

public sealed class BootstrapAdminOptionsValidator(IdentityOptions identityOptions)
    : IValidateOptions<BootstrapAdminOptions>
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    public ValidateOptionsResult Validate(string? name, BootstrapAdminOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Email) || !EmailValidator.IsValid(options.Email))
        {
            failures.Add("BootstrapAdmin:Email is missing or invalid.");
        }

        if (string.IsNullOrWhiteSpace(options.FullName) || options.FullName.Trim().Length > 150)
        {
            failures.Add("BootstrapAdmin:FullName is missing or invalid.");
        }

        if (!MeetsPasswordPolicy(options.Password, identityOptions.Password))
        {
            failures.Add("BootstrapAdmin:Password is missing or does not meet the configured Identity password policy.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool MeetsPasswordPolicy(string? password, PasswordOptions policy)
    {
        if (string.IsNullOrEmpty(password) || password.Length < policy.RequiredLength)
        {
            return false;
        }

        if (password.Distinct().Count() < policy.RequiredUniqueChars)
        {
            return false;
        }

        return (!policy.RequireDigit || password.Any(char.IsDigit))
            && (!policy.RequireLowercase || password.Any(char.IsLower))
            && (!policy.RequireUppercase || password.Any(char.IsUpper))
            && (!policy.RequireNonAlphanumeric || password.Any(character => !char.IsLetterOrDigit(character)));
    }
}
