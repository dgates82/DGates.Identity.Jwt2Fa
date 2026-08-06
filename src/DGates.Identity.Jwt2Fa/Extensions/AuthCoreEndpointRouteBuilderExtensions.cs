using DGates.Identity.Jwt2Fa.Dtos;
using DGates.Identity.Jwt2Fa.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>Endpoint mapping for the core module: register, login, login2fa, secure.</summary>
public static class AuthCoreEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the core auth endpoints under <paramref name="prefix"/> onto <see cref="IAuthCoreService{TUser}"/>.
    /// </summary>
    public static RouteGroupBuilder MapAuthCore<TUser>(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/auth")
        where TUser : IdentityUser, new()
    {
        var group = endpoints.MapGroup(prefix);

        group.MapPost("/register", async (RegisterRequestDto request, IAuthCoreService<TUser> service) =>
            (await service.RegisterAsync(request)).ToIResult());

        group.MapPost("/login", async (AuthRequestDto request, IAuthCoreService<TUser> service) =>
            (await service.LoginAsync(request)).ToIResult());

        group.MapPost("/login2fa", async (TwoFaAuthRequestDto request, IAuthCoreService<TUser> service) =>
            (await service.LoginTwoFactorAsync(request)).ToIResult());

        group.MapGet("/secure", () => Results.Ok("Got it!")).RequireAuthorization();

        return group;
    }
}
