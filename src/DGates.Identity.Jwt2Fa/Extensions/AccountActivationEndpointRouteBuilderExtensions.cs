using DGates.Identity.Jwt2Fa.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>Endpoint mapping for the account-activation module: getuserbyemail.</summary>
public static class AccountActivationEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps a self-or-admin-gated user lookup under <paramref name="prefix"/> onto
    /// <see cref="IAccountActivationService{TUser}"/>.
    /// </summary>
    public static RouteGroupBuilder MapAccountActivation<TUser>(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/auth")
        where TUser : IdentityUser
    {
        var group = endpoints.MapGroup(prefix);

        group.MapGet("/getuserbyemail", async (
            string email,
            HttpContext httpContext,
            IAccountActivationService<TUser> service) =>
            (await service.GetUserByEmailAsync(email, httpContext.User)).ToIResult()
        ).RequireAuthorization();

        return group;
    }
}
