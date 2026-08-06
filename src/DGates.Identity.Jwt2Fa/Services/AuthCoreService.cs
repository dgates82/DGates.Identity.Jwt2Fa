using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Dtos;
using DGates.Identity.Jwt2Fa.Extensions;
using DGates.Identity.Jwt2Fa.Jwt;
using DGates.Identity.Jwt2Fa.TwoFactor;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
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
    private readonly IActivationPolicy<TUser>? _activationPolicy;

    /// <summary>
    /// Creates the service. <paramref name="activationPolicy"/> is optional so <see cref="LoginAsync"/>
    /// honors it when <c>AddAccountActivation</c> registered one, without core requiring it.
    /// </summary>
    public AuthCoreService(
        UserManager<TUser> userManager,
        SignInManager<TUser> signInManager,
        IJwtTokenService<TUser> jwtTokenService,
        Jwt2FaUserProjector<TUser> userProjector,
        IEmailSender emailSender,
        IOptions<AuthCoreOptions> authCoreOptions,
        IActivationPolicy<TUser>? activationPolicy = null)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _userProjector = userProjector;
        _emailSender = emailSender;
        _authCoreOptions = authCoreOptions;
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
        var callbackUrl = _authCoreOptions.Value.EmailConfirmationCallbackUrl
            .Replace("{userId}", Uri.EscapeDataString(user.Id))
            .Replace("{code}", Uri.EscapeDataString(code));

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
    public async Task<Jwt2FaResult<AuthResponseDto>> LoginTwoFactorAsync(TwoFaAuthRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Don't reveal that the user does not exist.
            return Jwt2FaResult<AuthResponseDto>.Ok(new AuthResponseDto { IsAuthSuccessful = false });
        }

        var tokenProvider = TwoFactorProviderNames.Resolve(request.TwoFactorProvider);
        if (tokenProvider is null)
        {
            return Jwt2FaResult<AuthResponseDto>.Ok(
                new AuthResponseDto { IsAuthSuccessful = false, ErrorMessage = "Invalid Authentication Code" });
        }

        var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, tokenProvider, request.TwoFactorCode);
        if (!isValid)
        {
            return Jwt2FaResult<AuthResponseDto>.Ok(
                new AuthResponseDto { IsAuthSuccessful = false, ErrorMessage = "Invalid Authentication Code" });
        }

        var token = await IssueTokenAsync(user);
        return Jwt2FaResult<AuthResponseDto>.Ok(new AuthResponseDto
        {
            IsAuthSuccessful = true,
            Token = token,
            RequiresTwoFactor = true,
            User = _userProjector(user)
        });
    }

    private async Task<string> IssueTokenAsync(TUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var signingCredentials = _jwtTokenService.GetSigningCredentials();
        var claims = _jwtTokenService.GetClaims(user, roles);
        return _jwtTokenService.WriteToken(_jwtTokenService.GenerateToken(signingCredentials, claims));
    }
}
