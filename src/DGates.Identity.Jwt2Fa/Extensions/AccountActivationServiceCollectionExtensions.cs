using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>DI registration for the account-activation module.</summary>
public static class AccountActivationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAccountActivationService{TUser}"/>. Requires zero extra
    /// shape on <typeparamref name="TUser"/> — <c>getuserbyemail</c> doesn't itself
    /// depend on <see cref="IActivationPolicy{TUser}"/>. Pair with
    /// <see cref="AddDefaultActivationPolicy{TUser}"/> or your own
    /// <c>IActivationPolicy&lt;TUser&gt;</c> registration if you also want the core
    /// login endpoint to enforce activation.
    /// </summary>
    public static IServiceCollection AddAccountActivation<TUser>(this IServiceCollection services)
        where TUser : IdentityUser
    {
        services.AddScoped<IAccountActivationService<TUser>, AccountActivationService<TUser>>();
        return services;
    }

    /// <summary>
    /// Registers the default <see cref="PropertyBackedActivationPolicy{TUser}"/> as
    /// <see cref="IActivationPolicy{TUser}"/> for the common case: activation is just
    /// <typeparamref name="TUser"/>'s <see cref="IActivatableUser.IsActive"/> flag.
    /// Consumers whose activation logic is more than a single flag should skip this
    /// method and register their own <see cref="IActivationPolicy{TUser}"/> directly —
    /// the core login endpoint (see <c>AddAuthCore</c>) honors whichever policy is
    /// registered, if any, regardless of how it got there.
    /// </summary>
    public static IServiceCollection AddDefaultActivationPolicy<TUser>(this IServiceCollection services)
        where TUser : IdentityUser, IActivatableUser
    {
        services.TryAddScoped<IActivationPolicy<TUser>, PropertyBackedActivationPolicy<TUser>>();
        return services;
    }
}
