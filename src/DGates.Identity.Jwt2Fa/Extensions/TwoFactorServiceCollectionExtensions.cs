using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>DI registration for the 2FA module.</summary>
public static class TwoFactorServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITwoFactorService{TUser}"/>. Constrained to
    /// <see cref="IMultiFactorMethodUser"/> because the 2FA enrollment flow writes
    /// <see cref="IMultiFactorMethodUser.TwoFactorMethod"/>. Requires an
    /// <c>IEmailSender</c> (<c>Microsoft.AspNetCore.Identity.UI.Services</c>) and an
    /// <c>ISmsSender</c> (<c>DGates.Identity.NotificationProviders.Abstractions</c>)
    /// registered separately — this package doesn't ship notification providers itself.
    /// </summary>
    public static IServiceCollection Add2Fa<TUser>(this IServiceCollection services)
        where TUser : IdentityUser, IMultiFactorMethodUser
    {
        services.AddScoped<ITwoFactorService<TUser>, TwoFactorService<TUser>>();
        return services;
    }
}
