using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Dtos;
using DGates.Identity.Jwt2Fa.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Encodings.Web;

namespace DGates.Identity.Jwt2Fa.Services;

/// <inheritdoc cref="IAdminProvisioningService{TUser}" />
public sealed class AdminProvisioningService<TUser> : IAdminProvisioningService<TUser>
    where TUser : IdentityUser, IAdminProvisionableUser
{
    private readonly UserManager<TUser> _userManager;
    private readonly SignInManager<TUser> _signInManager;
    private readonly IEmailSender _emailSender;
    private readonly IOptions<AuthCoreOptions> _authCoreOptions;
    private readonly IOptions<AdminProvisioningOptions> _provisioningOptions;

    /// <summary>Creates the service with its user store, notification sender, and options.</summary>
    public AdminProvisioningService(
        UserManager<TUser> userManager,
        SignInManager<TUser> signInManager,
        IEmailSender emailSender,
        IOptions<AuthCoreOptions> authCoreOptions,
        IOptions<AdminProvisioningOptions> provisioningOptions)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
        _authCoreOptions = authCoreOptions;
        _provisioningOptions = provisioningOptions;
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<ResponseDto>> ForgotPasswordAsync(ForgotPasswordDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.IsEmailConfirmedAsync(user))
        {
            // Don't reveal that the user does not exist or is not confirmed.
            return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = false });
        }

        var code = await _userManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var appName = _authCoreOptions.Value.ApplicationName;
        var callbackUrl = _provisioningOptions.Value.ForgotPasswordCallbackUrl
            .Replace("{code}", Uri.EscapeDataString(code));

        await _emailSender.SendEmailAsync(
            request.Email,
            $"{appName} Password Reset",
            $"Forgot your password?<br/>We received a request to reset the password for your account.<br/><br/>" +
            $"To reset your password <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>click here</a>.<br/><br/>" +
            $"If you did not request a password reset please ignore this email.");

        return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = true });
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<ResponseDto>> ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Don't reveal that the user does not exist.
            return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = false });
        }

        var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Code));
        var result = await _userManager.ResetPasswordAsync(user, code, request.Password);

        if (result.Succeeded)
        {
            user.HasSetPassword = true;
            await _userManager.UpdateAsync(user);
        }

        return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto
        {
            IsSuccess = result.Succeeded,
            Message = result.Succeeded ? null : string.Join(" ", result.Errors.Select(e => e.Description))
        });
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<ResponseDto>> ChangePasswordAsync(ChangePasswordRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Don't reveal that the user does not exist.
            return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = false });
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
            return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = false, Message = errorMessage });
        }

        await _signInManager.RefreshSignInAsync(user);

        return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = true, Message = "Your password has been set." });
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<ResponseDto>> SendEmailConfirmationAsync(SendEmailConfirmationRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = false });
        }

        var emailCode = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        emailCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(emailCode));

        var appName = _authCoreOptions.Value.ApplicationName;
        var callbackUrl = _authCoreOptions.Value.EmailConfirmationCallbackUrl
            .Replace("{userId}", Uri.EscapeDataString(user.Id))
            .Replace("{code}", Uri.EscapeDataString(emailCode));

        // Admin-created accounts haven't set their own password yet — bundle a
        // password reset code into the same link so first login can set one.
        if (!user.HasSetPassword)
        {
            var passwordResetCode = await _userManager.GeneratePasswordResetTokenAsync(user);
            passwordResetCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(passwordResetCode));
            callbackUrl += $"&passwordResetCode={Uri.EscapeDataString(passwordResetCode)}&isFirstLogin=true";
        }

        await _emailSender.SendEmailAsync(
            request.Email,
            $"{appName} Email Confirmation",
            $"In order to start using {appName}, you need to verify your email.<br/><br/>" +
            $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.<br/><br/>" +
            $"If you did not request a login to {appName}, please ignore this email.");

        return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = true });
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<ResponseDto>> ConfirmEmailAsync(ConfirmEmailRequestDto request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null)
        {
            // Don't reveal that the user does not exist.
            return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = false });
        }

        var emailCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Code));
        var result = await _userManager.ConfirmEmailAsync(user, emailCode);

        return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = result.Succeeded });
    }
}
