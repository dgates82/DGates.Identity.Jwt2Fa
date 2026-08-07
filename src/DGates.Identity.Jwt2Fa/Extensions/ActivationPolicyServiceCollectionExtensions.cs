using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>
/// DI registration for the optional account-activation policy and its admin
/// activate/deactivate service. Not a module in the same sense as core/two-factor —
/// core's <c>login</c> honors whichever <see cref="IActivationPolicy{TUser}"/> is
/// registered, if any, regardless of how it got there — but this method's matching
/// <c>MapDefaultActivationPolicy</c> maps admin endpoints for the single-flag case
/// specifically, since only that case has an unambiguous "set" operation.
/// </summary>
public static class ActivationPolicyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default <see cref="PropertyBackedActivationPolicy{TUser}"/> as
    /// <see cref="IActivationPolicy{TUser}"/>, and <see cref="IUserActivationService{TUser}"/>
    /// for the admin activate/deactivate endpoints, for the common case: activation is
    /// just <typeparamref name="TUser"/>'s <see cref="IActivatableUser.IsActive"/> flag.
    /// Consumers whose activation logic is more than a single flag should skip this
    /// method and register their own <see cref="IActivationPolicy{TUser}"/> directly —
    /// core's <c>login</c> honors whichever policy is registered, if any, regardless of
    /// how it got there — and their own admin action for toggling it.
    /// </summary>
    public static IServiceCollection AddDefaultActivationPolicy<TUser>(this IServiceCollection services)
        where TUser : IdentityUser, IActivatableUser
    {
        services.TryAddScoped<IActivationPolicy<TUser>, PropertyBackedActivationPolicy<TUser>>();
        services.TryAddScoped<IUserActivationService<TUser>, UserActivationService<TUser>>();
        return services;
    }
}
