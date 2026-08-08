using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Dtos;
using DGates.Identity.Jwt2Fa.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>
/// Endpoint mapping for the 2FA module: login2fa, SendTwoFaCode, enableauthenticator,
/// verifyauthenticator, resetauthenticator.
/// </summary>
public static class TwoFactorEndpointRouteBuilderExtensions
{
    /// <summary>Maps the 2FA endpoints under <paramref name="prefix"/> onto <see cref="ITwoFactorService{TUser}"/>.</summary>
    public static RouteGroupBuilder Map2Fa<TUser>(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/auth")
        where TUser : IdentityUser, IMultiFactorMethodUser
    {
        var group = endpoints.MapGroup(prefix)
            .AddEndpointFilter<ExceptionHandlingEndpointFilter>()
            .AddEndpointFilter<ValidationEndpointFilter>();

        group.MapPost("/login2fa", async (TwoFaAuthRequestDto request, ITwoFactorService<TUser> service) =>
            (await service.LoginTwoFactorAsync(request)).ToIResult());

        group.MapPost("/sendtwofacode", async (
            SendVerificationCodeRequestDto request,
            HttpContext httpContext,
            ITwoFactorService<TUser> service) =>
            (await service.SendTwoFaCodeAsync(request, httpContext.User)).ToIResult());

        group.MapPost("/enableauthenticator", async (
            EnableAuthenticatorRequestDto request,
            HttpContext httpContext,
            ITwoFactorService<TUser> service) =>
            (await service.EnableAuthenticatorAsync(request, httpContext.User)).ToIResult())
            .RequireAuthorization();

        group.MapPost("/verifyauthenticator", async (
            VerifyAuthenticatorRequestDto request,
            HttpContext httpContext,
            ITwoFactorService<TUser> service) =>
            (await service.VerifyAuthenticatorAsync(request, httpContext.User)).ToIResult())
            .RequireAuthorization();

        group.MapPost("/resetauthenticator", async (
            EnableAuthenticatorRequestDto request,
            HttpContext httpContext,
            ITwoFactorService<TUser> service) =>
            (await service.ResetAuthenticatorAsync(request, httpContext.User)).ToIResult())
            .RequireAuthorization();

        return group;
    }
}
