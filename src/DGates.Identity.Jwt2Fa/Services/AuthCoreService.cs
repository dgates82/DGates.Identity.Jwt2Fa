using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Dtos;
using DGates.Identity.Jwt2Fa.Extensions;
using DGates.Identity.Jwt2Fa.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace DGates.Identity.Jwt2Fa.Services;

/// <inheritdoc cref="IAuthCoreService{TUser}" />
public sealed class AuthCoreService<TUser> : IAuthCoreService<TUser>
    where TUser : IdentityUser, new()
{
    private readonly UserManager<TUser> _userManager;
    private readonly SignInManager<TUser> _signInManager;
    private readonly IJwtTokenService<TUser> _jwtTokenService;
    private readonly Jwt2FaUserProjector<TUser> _userProjector;
    private readonly IEmailSender _emailSender;
    private readonly IOptions<AuthCoreOptions> _authCoreOptions;
    private readonly IOptions<JwtOptions> _jwtOptions;
    private readonly IActivationPolicy<TUser>? _activationPolicy;

    /// <summary>
    /// Creates the service. <paramref name="activationPolicy"/> is optional so <see cref="LoginAsync"/>
    /// honors it when a policy is registered (e.g. via <c>AddDefaultActivationPolicy</c> or your
    /// own), without core requiring it.
    /// </summary>
    public AuthCoreService(
        UserManager<TUser> userManager,
        SignInManager<TUser> signInManager,
        IJwtTokenService<TUser> jwtTokenService,
        Jwt2FaUserProjector<TUser> userProjector,
        IEmailSender emailSender,
        IOptions<AuthCoreOptions> authCoreOptions,
        IOptions<JwtOptions> jwtOptions,
        IActivationPolicy<TUser>? activationPolicy = null)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _userProjector = userProjector;
        _emailSender = emailSender;
        _authCoreOptions = authCoreOptions;
        _jwtOptions = jwtOptions;
        _activationPolicy = activationPolicy;
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<ResponseDto>> RegisterAsync(RegisterRequestDto request)
    {
        var user = new TUser { UserName = request.Email, Email = request.Email };
        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return Jwt2FaResult<ResponseDto>.BadRequest(result.Errors);
        }

        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var appName = _authCoreOptions.Value.ApplicationName;
        var callbackUrl = BuildUrl(_authCoreOptions.Value.EmailConfirmationPath,
            ("userId", user.Id), ("code", code));

        await _emailSender.SendEmailAsync(
            request.Email,
            $"{appName} Email Confirmation",
            $"In order to start using {appName}, you need to verify your email.<br/><br/>" +
            $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.<br/><br/>" +
            $"If you did not request a login to {appName}, please ignore this email.");

        return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = true });
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<AuthResponseDto>> LoginAsync(AuthRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Don't reveal that the user does not exist.
            return Jwt2FaResult<AuthResponseDto>.Ok(new AuthResponseDto { IsAuthSuccessful = false });
        }

        if (_activationPolicy is not null && !_activationPolicy.IsActive(user))
        {
            return Jwt2FaResult<AuthResponseDto>.Ok(
                new AuthResponseDto { IsAuthSuccessful = false, ErrorMessage = "User is not active" });
        }

        var result = await _signInManager.PasswordSignInAsync(
            request.Email, request.Password, isPersistent: false, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            var token = await IssueTokenAsync(user);
            return Jwt2FaResult<AuthResponseDto>.Ok(new AuthResponseDto
            {
                IsAuthSuccessful = true,
                Token = token,
                RequiresTwoFactor = user.TwoFactorEnabled,
                User = _userProjector(user)
            });
        }

        if (result.RequiresTwoFactor)
        {
            var twoFactorMethod = (user as IMultiFactorMethodUser)?.TwoFactorMethod;
            return Jwt2FaResult<AuthResponseDto>.Ok(new AuthResponseDto
            {
                IsAuthSuccessful = false,
                RequiresTwoFactor = true,
                TwoFactorMethod = twoFactorMethod ?? "",
                PhoneNumber = twoFactorMethod == "Phone" ? user.PhoneNumber ?? "" : ""
            });
        }

        await _userManager.AccessFailedAsync(user);
        return Jwt2FaResult<AuthResponseDto>.Unauthorized(
            new AuthResponseDto { IsAuthSuccessful = false, ErrorMessage = "Invalid Authentication" });
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
        var callbackUrl = BuildUrl(_authCoreOptions.Value.ForgotPasswordPath, ("code", code));

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

        if (result.Succeeded && user is IAdminProvisionableUser provisionable)
        {
            provisionable.HasSetPassword = true;
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
        var callbackUrl = BuildUrl(_authCoreOptions.Value.EmailConfirmationPath,
            ("userId", user.Id), ("code", emailCode));

        // Admin-created accounts haven't set their own password yet — bundle a
        // password reset code into the same link so first login can set one. Only
        // meaningful (and only checked) if TUser tracks that state at all.
        if (user is IAdminProvisionableUser { HasSetPassword: false })
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

    /// <inheritdoc />
    public async Task<Jwt2FaResult<object>> GetUserByEmailAsync(string email, ClaimsPrincipal caller)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Jwt2FaResult<object>.Ok(new ResponseDto { IsSuccess = false });
        }

        var currentUser = await _userManager.GetUserAsync(caller);
        var isSelf = currentUser is not null && currentUser.Id == user.Id;
        var isAdmin = currentUser is not null
            && await _userManager.IsInRoleAsync(currentUser, _jwtOptions.Value.AdminRoleName);

        if (!isSelf && !isAdmin)
        {
            return Jwt2FaResult<object>.BadRequest(
                "You can only look up your own account. Looking up another account requires the admin role.");
        }

        return Jwt2FaResult<object>.Ok(_userProjector(user));
    }

    private string BuildUrl(string pathTemplate, params (string Token, string Value)[] substitutions)
    {
        var url = _authCoreOptions.Value.FrontendBaseUrl + pathTemplate;
        foreach (var (token, value) in substitutions)
        {
            url = url.Replace($"{{{token}}}", Uri.EscapeDataString(value));
        }
        return url;
    }

    private async Task<string> IssueTokenAsync(TUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var signingCredentials = _jwtTokenService.GetSigningCredentials();
        var claims = _jwtTokenService.GetClaims(user, roles);
        return _jwtTokenService.WriteToken(_jwtTokenService.GenerateToken(signingCredentials, claims));
    }
}
