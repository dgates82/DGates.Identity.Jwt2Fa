using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>DI registration for the admin-provisioning module.</summary>
public static class AdminProvisioningServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="AdminProvisioningOptions"/> and registers
    /// <see cref="IAdminProvisioningService{TUser}"/>. Constrained to
    /// <see cref="IAdminProvisionableUser"/> because <c>resetpassword</c> writes
    /// <see cref="IAdminProvisionableUser.HasSetPassword"/> and <c>sendemailconfirmation</c>
    /// branches on it for the admin-created-account first-login flow.
    /// </summary>
    public static IServiceCollection AddAdminProvisioning<TUser>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TUser : IdentityUser, IAdminProvisionableUser
    {
        services.Configure<AdminProvisioningOptions>(configuration.GetSection(AdminProvisioningOptions.ConfigSection));
        services.AddScoped<IAdminProvisioningService<TUser>, AdminProvisioningService<TUser>>();
        return services;
    }
}
