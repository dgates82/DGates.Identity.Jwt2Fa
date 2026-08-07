using DGates.Identity.Jwt2Fa.Dtos;
using DGates.Identity.Jwt2Fa.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>
/// Endpoint mapping for the core module: register, login, secure, and the password/email
/// lifecycle (forgotpassword, resetpassword, changepassword, sendemailconfirmation, confirmEmail).
/// </summary>
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

        group.MapGet("/secure", () => Results.Ok("Got it!")).RequireAuthorization();

        group.MapPost("/forgotpassword", async (ForgotPasswordDto request, IAuthCoreService<TUser> service) =>
            (await service.ForgotPasswordAsync(request)).ToIResult());

        group.MapPost("/resetpassword", async (ResetPasswordRequestDto request, IAuthCoreService<TUser> service) =>
            (await service.ResetPasswordAsync(request)).ToIResult());

        group.MapPost("/changepassword", async (ChangePasswordRequestDto request, IAuthCoreService<TUser> service) =>
            (await service.ChangePasswordAsync(request)).ToIResult())
            .RequireAuthorization();

        group.MapPost("/sendemailconfirmation", async (SendEmailConfirmationRequestDto request, IAuthCoreService<TUser> service) =>
            (await service.SendEmailConfirmationAsync(request)).ToIResult());

        group.MapPost("/confirmEmail", async (ConfirmEmailRequestDto request, IAuthCoreService<TUser> service) =>
            (await service.ConfirmEmailAsync(request)).ToIResult());

        return group;
    }
}
