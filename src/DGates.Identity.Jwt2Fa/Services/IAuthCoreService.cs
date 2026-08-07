using DGates.Identity.Jwt2Fa.Dtos;
using Microsoft.AspNetCore.Identity;

namespace DGates.Identity.Jwt2Fa.Services;

/// <summary>
/// Core auth logic: register, login, and the password/email lifecycle (forgot/reset/change
/// password, email confirmation). These need nothing beyond <see cref="IdentityUser"/> to
/// work, so they aren't gated behind any capability interface — see
/// <see cref="Capabilities.IAdminProvisionableUser"/> for the one place a capability
/// opportunistically enhances behavior here (<see cref="ResetPasswordAsync"/> writes it if
/// present; <see cref="SendEmailConfirmationAsync"/> reads it if present).
/// </summary>
public interface IAuthCoreService<TUser>
    where TUser : IdentityUser
{
    /// <summary>Creates a new account and emails a confirmation link.</summary>
    Task<Jwt2FaResult<ResponseDto>> RegisterAsync(RegisterRequestDto request);

    /// <summary>
    /// Validates credentials and, if 2FA isn't required, issues a JWT. If 2FA is
    /// required, completing it is <c>ITwoFactorService.LoginTwoFactorAsync</c>'s job —
    /// nothing can ever require 2FA without the <c>Add2Fa</c> module having enabled it.
    /// </summary>
    Task<Jwt2FaResult<AuthResponseDto>> LoginAsync(AuthRequestDto request);

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
