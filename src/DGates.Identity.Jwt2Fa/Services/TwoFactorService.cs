using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Dtos;
using DGates.Identity.Jwt2Fa.Extensions;
using DGates.Identity.Jwt2Fa.Jwt;
using DGates.Identity.Jwt2Fa.TwoFactor;
using DGates.Identity.NotificationProviders.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace DGates.Identity.Jwt2Fa.Services;

/// <inheritdoc cref="ITwoFactorService{TUser}" />
public sealed class TwoFactorService<TUser> : ITwoFactorService<TUser>
    where TUser : IdentityUser, IMultiFactorMethodUser
{
    private readonly UserManager<TUser> _userManager;
    private readonly SignInManager<TUser> _signInManager;
    private readonly IJwtTokenService<TUser> _jwtTokenService;
    private readonly Jwt2FaUserProjector<TUser> _userProjector;
    private readonly IEmailSender _emailSender;
    private readonly ISmsSender _smsSender;
    private readonly IOptions<AuthCoreOptions> _authCoreOptions;
    private readonly IOptions<JwtOptions> _jwtOptions;

    /// <summary>Creates the service with its user store, notification senders, and options.</summary>
    public TwoFactorService(
        UserManager<TUser> userManager,
        SignInManager<TUser> signInManager,
        IJwtTokenService<TUser> jwtTokenService,
        Jwt2FaUserProjector<TUser> userProjector,
        IEmailSender emailSender,
        ISmsSender smsSender,
        IOptions<AuthCoreOptions> authCoreOptions,
        IOptions<JwtOptions> jwtOptions)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _userProjector = userProjector;
        _emailSender = emailSender;
        _smsSender = smsSender;
        _authCoreOptions = authCoreOptions;
        _jwtOptions = jwtOptions;
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
        await PopulateRolesIfAwareAsync(user);
        return Jwt2FaResult<AuthResponseDto>.Ok(new AuthResponseDto
        {
            IsAuthSuccessful = true,
            Token = token,
            RequiresTwoFactor = true,
            User = _userProjector(user)
        });
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<ResponseDto>> SendTwoFaCodeAsync(SendVerificationCodeRequestDto request, ClaimsPrincipal caller)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = false });
        }

        var tokenProvider = TwoFactorProviderNames.Resolve(request.Method);
        if (string.IsNullOrEmpty(tokenProvider))
        {
            return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = false });
        }

        // A Phone send trusts request.PhoneNumber as the delivery target whenever the
        // account isn't already verified for Phone specifically - a fresh enrollment and
        // a switch from another already-enabled method both introduce an unverified
        // number, so both need the same self-or-admin check.
        var isUnverifiedPhoneTarget = tokenProvider == "Phone" && user.TwoFactorMethod != "Phone";
        if (!user.TwoFactorEnabled || isUnverifiedPhoneTarget)
        {
            var authFailure = await CheckSelfOrAdminAsync<ResponseDto>(user, caller,
                "You can only send a 2FA setup code to your own account. Doing this for another account requires the admin role.");
            if (authFailure is not null)
            {
                return authFailure.Value;
            }
        }

        var code = await _userManager.GenerateTwoFactorTokenAsync(user, tokenProvider);
        var appName = _authCoreOptions.Value.ApplicationName;

        switch (tokenProvider)
        {
            case "Authenticator":
                // Authenticator cannot be used to send codes.
                return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = false });
            case "Email":
                // No stated expiry — Identity's built-in Email/Phone providers use a fixed, unreadable internal window.
                await _emailSender.SendEmailAsync(
                    request.Email,
                    MessageTemplateFormatter.FormatHtml(_authCoreOptions.Value.TwoFactorCodeEmailSubject, ("applicationName", appName)),
                    MessageTemplateFormatter.FormatHtml(_authCoreOptions.Value.TwoFactorCodeEmailBody, ("code", code)));
                break;
            case "Phone":
                var phoneNumber = user.TwoFactorMethod == "Phone" ? user.PhoneNumber : request.PhoneNumber;
                if (string.IsNullOrEmpty(phoneNumber))
                {
                    return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = false });
                }
                await _smsSender.SendSmsAsync(
                    phoneNumber,
                    MessageTemplateFormatter.FormatPlainText(_authCoreOptions.Value.TwoFactorCodeSmsBody,
                        ("applicationName", appName), ("code", code)));
                break;
        }

        return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = true });
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<object>> EnableAuthenticatorAsync(EnableAuthenticatorRequestDto request, ClaimsPrincipal caller)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Jwt2FaResult<object>.Ok(new ResponseDto { IsSuccess = false });
        }

        var authFailure = await CheckSelfOrAdminAsync<object>(user, caller,
            "You can only enable your own authenticator. Enabling another account's authenticator requires the admin role.");
        if (authFailure is not null)
        {
            return authFailure.Value;
        }

        var unformattedKey = await GetOrCreateUnformattedKey(user);
        var formattedKey = AuthenticatorKeyFormatter.FormatKey(unformattedKey);
        var email = await _userManager.GetEmailAsync(user) ?? request.Email;
        var authenticatorUri = AuthenticatorKeyFormatter.BuildAuthenticatorUri(
            _authCoreOptions.Value.ApplicationName, email, unformattedKey);

        return Jwt2FaResult<object>.Ok(new EnableAuthenticatorResponseDto
        {
            SharedKey = formattedKey,
            AuthenticatorUri = authenticatorUri
        });
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<object>> VerifyAuthenticatorAsync(VerifyAuthenticatorRequestDto request, ClaimsPrincipal caller)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Jwt2FaResult<object>.Ok(new ResponseDto { IsSuccess = false });
        }

        var authFailure = await CheckSelfOrAdminAsync<object>(user, caller,
            "You can only verify your own authenticator. Verifying another account's authenticator requires the admin role.");
        if (authFailure is not null)
        {
            return authFailure.Value;
        }

        var tokenProvider = TwoFactorProviderNames.Resolve(request.Method);
        if (string.IsNullOrEmpty(tokenProvider))
        {
            return Jwt2FaResult<object>.Ok(new ResponseDto { IsSuccess = false });
        }

        var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, tokenProvider, request.Code);
        if (!isValid)
        {
            return Jwt2FaResult<object>.Ok(new VerifyAuthenticatorResponseDto
            {
                IsVerified = false,
                Message = "Could not verify authentication code."
            });
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        user.TwoFactorMethod = tokenProvider;
        if (tokenProvider == "Phone")
        {
            user.PhoneNumber = request.PhoneNumber;
        }
        await _userManager.UpdateAsync(user);

        var response = new VerifyAuthenticatorResponseDto
        {
            IsVerified = true,
            Message = "Your 2FA authentication has been verified."
        };

        if (tokenProvider == "Authenticator")
        {
            var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            response.Codes = recoveryCodes?.ToArray() ?? Array.Empty<string>();
        }

        return Jwt2FaResult<object>.Ok(response);
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<ResponseDto>> ResetAuthenticatorAsync(EnableAuthenticatorRequestDto request, ClaimsPrincipal caller)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Jwt2FaResult<ResponseDto>.NotFound($"Unable to load user with email '{request.Email}'.");
        }

        var authFailure = await CheckSelfOrAdminAsync<ResponseDto>(user, caller,
            "You can only reset your own authenticator. Resetting another account's authenticator requires the admin role.");
        if (authFailure is not null)
        {
            return authFailure.Value;
        }

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
        user.TwoFactorMethod = null;
        await _userManager.UpdateAsync(user);

        await _signInManager.RefreshSignInAsync(user);

        return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto
        {
            IsSuccess = true,
            Message = "Authenticator app key has been reset. User will need to configure their authenticator app using a new key."
        });
    }

    /// <summary>Returns a populated <see cref="Jwt2FaResult{T}"/> if the caller is neither the target user nor an admin, otherwise <c>null</c>.</summary>
    private async Task<Jwt2FaResult<T>?> CheckSelfOrAdminAsync<T>(TUser user, ClaimsPrincipal caller, string forbiddenMessage)
    {
        var currentUser = await _userManager.GetUserAsync(caller);
        var isSelf = currentUser is not null && currentUser.Id == user.Id;
        var isAdmin = currentUser is not null
            && await _userManager.IsInRoleAsync(currentUser, _jwtOptions.Value.AdminRoleName);

        return !isSelf && !isAdmin ? Jwt2FaResult<T>.BadRequest(forbiddenMessage) : null;
    }

    private async Task PopulateRolesIfAwareAsync(TUser user)
    {
        if (user is IRoleAwareUser roleAware)
        {
            roleAware.Roles = (await _userManager.GetRolesAsync(user)).ToList();
        }
    }

    private async Task<string> IssueTokenAsync(TUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var signingCredentials = _jwtTokenService.GetSigningCredentials();
        var claims = _jwtTokenService.GetClaims(user, roles);
        return _jwtTokenService.WriteToken(_jwtTokenService.GenerateToken(signingCredentials, claims));
    }

    private async Task<string> GetOrCreateUnformattedKey(TUser user)
    {
        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        return unformattedKey
            ?? throw new InvalidOperationException($"Failed to generate an authenticator key for user '{user.Id}'.");
    }
}
