using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Dtos;
using Microsoft.AspNetCore.Identity;

namespace DGates.Identity.Jwt2Fa.Services;

/// <summary>Admin-provisioning logic: password reset/change and email-confirmation flows.</summary>
public interface IAdminProvisioningService<TUser>
    where TUser : IdentityUser, IAdminProvisionableUser
{
    /// <summary>Emails a password reset link for a confirmed account.</summary>
    Task<Jwt2FaResult<ResponseDto>> ForgotPasswordAsync(ForgotPasswordDto request);

    /// <summary>Sets a new password using the reset code emailed by <see cref="ForgotPasswordAsync"/>.</summary>
    Task<Jwt2FaResult<ResponseDto>> ResetPasswordAsync(ResetPasswordRequestDto request);

    /// <summary>Changes a user's password given their current password.</summary>
    Task<Jwt2FaResult<ResponseDto>> ChangePasswordAsync(ChangePasswordRequestDto request);

    /// <summary>Resends the email confirmation link for a user.</summary>
    Task<Jwt2FaResult<ResponseDto>> SendEmailConfirmationAsync(SendEmailConfirmationRequestDto request);

    /// <summary>Confirms a user's email using the code from the confirmation link.</summary>
    Task<Jwt2FaResult<ResponseDto>> ConfirmEmailAsync(ConfirmEmailRequestDto request);
}
