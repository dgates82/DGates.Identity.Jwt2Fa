using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Dtos;
using DGates.Identity.Jwt2Fa.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>
/// Endpoint mapping for the admin-provisioning module: forgotpassword, resetpassword,
/// changepassword, sendemailconfirmation, confirmEmail.
/// </summary>
public static class AdminProvisioningEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the admin-provisioning endpoints under <paramref name="prefix"/> onto
    /// <see cref="IAdminProvisioningService{TUser}"/>.
    /// </summary>
    public static RouteGroupBuilder MapAdminProvisioning<TUser>(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/auth")
        where TUser : IdentityUser, IAdminProvisionableUser
    {
        var group = endpoints.MapGroup(prefix);

        group.MapPost("/forgotpassword", async (ForgotPasswordDto request, IAdminProvisioningService<TUser> service) =>
            (await service.ForgotPasswordAsync(request)).ToIResult());

        group.MapPost("/resetpassword", async (ResetPasswordRequestDto request, IAdminProvisioningService<TUser> service) =>
            (await service.ResetPasswordAsync(request)).ToIResult());

        group.MapPost("/changepassword", async (ChangePasswordRequestDto request, IAdminProvisioningService<TUser> service) =>
            (await service.ChangePasswordAsync(request)).ToIResult())
            .RequireAuthorization();

        group.MapPost("/sendemailconfirmation", async (SendEmailConfirmationRequestDto request, IAdminProvisioningService<TUser> service) =>
            (await service.SendEmailConfirmationAsync(request)).ToIResult());

        group.MapPost("/confirmEmail", async (ConfirmEmailRequestDto request, IAdminProvisioningService<TUser> service) =>
            (await service.ConfirmEmailAsync(request)).ToIResult());

        return group;
    }
}
