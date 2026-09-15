using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace Microsoft.Extensions.DependencyInjection;

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddVeterinaryAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization();
        services.Replace(
            ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAuthorizationHandler, PermissionAuthorizationHandler>());
        services.TryAddScoped<IUserPermissionStore, EfUserPermissionStore>();
        services.TryAddScoped<IPermissionService, PermissionService>();

        return services;
    }
}
