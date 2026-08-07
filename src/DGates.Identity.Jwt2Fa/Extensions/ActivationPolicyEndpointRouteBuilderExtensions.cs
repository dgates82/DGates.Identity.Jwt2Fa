using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>
/// Endpoint mapping for the admin activate/deactivate actions backed by
/// <see cref="IUserActivationService{TUser}"/> — the <see cref="IActivatableUser"/>
/// single-flag case only, registered by <c>AddDefaultActivationPolicy</c>.
/// </summary>
public static class ActivationPolicyEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps <c>activate</c>/<c>deactivate</c> under <paramref name="prefix"/>, both
    /// requiring <see cref="Jwt2FaPolicies.AdminOnly"/>.
    /// </summary>
    public static RouteGroupBuilder MapDefaultActivationPolicy<TUser>(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/auth")
        where TUser : IdentityUser, IActivatableUser
    {
        var group = endpoints.MapGroup(prefix);

        group.MapPost("/activate/{id}", async (string id, IUserActivationService<TUser> service) =>
            (await service.ActivateAsync(id)).ToIResult()
        ).RequireAuthorization(Jwt2FaPolicies.AdminOnly);

        group.MapPost("/deactivate/{id}", async (string id, IUserActivationService<TUser> service) =>
            (await service.DeactivateAsync(id)).ToIResult()
        ).RequireAuthorization(Jwt2FaPolicies.AdminOnly);

        return group;
    }
}
