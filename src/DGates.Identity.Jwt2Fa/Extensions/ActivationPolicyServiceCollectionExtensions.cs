using DGates.Identity.Jwt2Fa.Capabilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>
/// DI registration for the optional account-activation policy. Not a module of its own —
/// there's no matching <c>Map</c> method, since core's <c>login</c> honors whichever
/// <see cref="IActivationPolicy{TUser}"/> is registered, if any, regardless of how it got
/// there. Calling this method is just a convenience for the common single-flag case.
/// </summary>
public static class ActivationPolicyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default <see cref="PropertyBackedActivationPolicy{TUser}"/> as
    /// <see cref="IActivationPolicy{TUser}"/> for the common case: activation is just
    /// <typeparamref name="TUser"/>'s <see cref="IActivatableUser.IsActive"/> flag.
    /// Consumers whose activation logic is more than a single flag should skip this
    /// method and register their own <see cref="IActivationPolicy{TUser}"/> directly —
    /// core's <c>login</c> honors whichever policy is registered, if any, regardless of
    /// how it got there.
    /// </summary>
    public static IServiceCollection AddDefaultActivationPolicy<TUser>(this IServiceCollection services)
        where TUser : IdentityUser, IActivatableUser
    {
        services.TryAddScoped<IActivationPolicy<TUser>, PropertyBackedActivationPolicy<TUser>>();
        return services;
    }
}
