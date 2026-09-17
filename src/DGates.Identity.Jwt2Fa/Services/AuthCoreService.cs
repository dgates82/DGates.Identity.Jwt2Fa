using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Dtos;
using DGates.Identity.Jwt2Fa.Extensions;
using DGates.Identity.Jwt2Fa.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
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

    private const int DefaultPageSize = 20;
    private const int TemporaryPasswordLength = 24;
    private const string UserIdTokenName = "userId";
    private const string ApplicationNameTokenName = "applicationName";

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

        // Self-registration means the account holder set this password themselves,
        // unlike AdminCreateUserAsync's discarded temporary one.
        if (user is IAdminProvisionableUser provisionable)
        {
            provisionable.HasSetPassword = true;
            await _userManager.UpdateAsync(user);
        }

        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var appName = _authCoreOptions.Value.ApplicationName;
        var callbackUrl = BuildUrl(_authCoreOptions.Value.EmailConfirmationPath,
            (UserIdTokenName, user.Id), ("code", code));

        await SendEmailConfirmationEmailAsync(request.Email, appName, callbackUrl);

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
            await PopulateRolesIfAwareAsync(user);
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
        var callbackUrl = BuildUrl(_authCoreOptions.Value.ForgotPasswordPath, ("code", code), (UserIdTokenName, user.Id));

        // Reissues admincreateuser's account-setup email, not this method's own "forgot password" wording.
        if (user is IAdminProvisionableUser { HasSetPassword: false })
        {
            callbackUrl += "&isFirstLogin=true";
            await SendAccountSetupEmailAsync(request.Email, appName, callbackUrl);
            return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = true });
        }

        await _emailSender.SendEmailAsync(
            request.Email,
            MessageTemplateFormatter.FormatHtml(_authCoreOptions.Value.ForgotPasswordEmailSubject, (ApplicationNameTokenName, appName)),
            MessageTemplateFormatter.FormatHtml(_authCoreOptions.Value.ForgotPasswordEmailBody,
                (ApplicationNameTokenName, appName), ("link", callbackUrl)));

        return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = true });
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<ResponseDto>> ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
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
    public async Task<Jwt2FaResult<ResponseDto>> ChangePasswordAsync(ChangePasswordRequestDto request, ClaimsPrincipal caller)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Don't reveal that the user does not exist.
            return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto { IsSuccess = false });
        }

        var currentUser = await _userManager.GetUserAsync(caller);
        var isSelf = currentUser is not null && currentUser.Id == user.Id;
        var isAdmin = currentUser is not null
            && await _userManager.IsInRoleAsync(currentUser, _jwtOptions.Value.AdminRoleName);

        if (!isSelf && !isAdmin)
        {
            return Jwt2FaResult<ResponseDto>.BadRequest(
                "You can only change your own password. Changing another account's password requires the admin role.");
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
            (UserIdTokenName, user.Id), ("code", emailCode));

        // Admin-created accounts haven't set their own password yet — bundle a
        // password reset code into the same link so first login can set one. Only
        // meaningful (and only checked) if TUser tracks that state at all.
        if (user is IAdminProvisionableUser { HasSetPassword: false })
        {
            var passwordResetCode = await _userManager.GeneratePasswordResetTokenAsync(user);
            passwordResetCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(passwordResetCode));
            callbackUrl += $"&passwordResetCode={Uri.EscapeDataString(passwordResetCode)}&isFirstLogin=true";
        }

        await SendEmailConfirmationEmailAsync(request.Email, appName, callbackUrl);

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

        await PopulateRolesIfAwareAsync(user);

        return Jwt2FaResult<object>.Ok(_userProjector(user));
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<object>> GetUserByIdAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return Jwt2FaResult<object>.NotFound($"No user found with id '{id}'.");
        }

        await PopulateRolesIfAwareAsync(user);

        return Jwt2FaResult<object>.Ok(_userProjector(user));
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<PagedResultDto<object>>> ListUsersAsync(int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? DefaultPageSize : pageSize;
        pageSize = Math.Min(pageSize, _authCoreOptions.Value.MaxPageSize);

        // UserManager.Users is a plain IQueryable<TUser> with no async guarantee unless the
        // consumer's store happens to be EF Core — this package doesn't take an EF Core
        // dependency itself, so pagination here is synchronous LINQ, not .ToListAsync().
        var query = _userManager.Users.OrderBy(u => u.Email);
        var totalCount = query.Count();
        var users = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        foreach (var user in users)
        {
            await PopulateRolesIfAwareAsync(user);
        }

        return Jwt2FaResult<PagedResultDto<object>>.Ok(new PagedResultDto<object>
        {
            Items = users.Select(u => _userProjector(u)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<object>> AdminCreateUserAsync(AdminCreateUserRequestDto request)
    {
        var user = new TUser { UserName = request.Email, Email = request.Email };
        var temporaryPassword = GenerateTemporaryPassword();
        var result = await _userManager.CreateAsync(user, temporaryPassword);

        if (!result.Succeeded)
        {
            return Jwt2FaResult<object>.BadRequest(result.Errors);
        }

        foreach (var role in request.Roles)
        {
            await _userManager.AddToRoleAsync(user, role);
        }

        if (user is IAdminProvisionableUser provisionable)
        {
            provisionable.HasSetPassword = false;
            await _userManager.UpdateAsync(user);
        }

        var emailCode = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        emailCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(emailCode));

        var appName = _authCoreOptions.Value.ApplicationName;
        var callbackUrl = BuildUrl(_authCoreOptions.Value.EmailConfirmationPath,
            (UserIdTokenName, user.Id), ("code", emailCode));

        if (user is IAdminProvisionableUser { HasSetPassword: false })
        {
            var passwordResetCode = await _userManager.GeneratePasswordResetTokenAsync(user);
            passwordResetCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(passwordResetCode));
            callbackUrl += $"&passwordResetCode={Uri.EscapeDataString(passwordResetCode)}&isFirstLogin=true";
        }

        await SendAccountSetupEmailAsync(request.Email, appName, callbackUrl);

        await PopulateRolesIfAwareAsync(user);

        return Jwt2FaResult<object>.Ok(_userProjector(user));
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<object>> AdminUpdateUserAsync(string id, AdminUpdateUserRequestDto request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return Jwt2FaResult<object>.NotFound($"No user found with id '{id}'.");
        }

        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await _userManager.SetEmailAsync(user, request.Email);
            if (!emailResult.Succeeded)
            {
                return Jwt2FaResult<object>.BadRequest(emailResult.Errors);
            }

            var userNameResult = await _userManager.SetUserNameAsync(user, request.Email);
            if (!userNameResult.Succeeded)
            {
                return Jwt2FaResult<object>.BadRequest(userNameResult.Errors);
            }

            // SetEmailAsync resets EmailConfirmed to false — without re-sending confirmation,
            // an admin renaming a user's email would silently strand them unable to log in
            // under RequireConfirmedAccount.
            var emailCode = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            emailCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(emailCode));

            var appName = _authCoreOptions.Value.ApplicationName;
            var callbackUrl = BuildUrl(_authCoreOptions.Value.EmailConfirmationPath,
                (UserIdTokenName, user.Id), ("code", emailCode));

            await _emailSender.SendEmailAsync(
                request.Email,
                $"{appName} Email Confirmation",
                $"Your email address on {appName} was just changed to this address by an administrator.<br/><br/>" +
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.<br/><br/>" +
                $"If you did not expect this change, please contact your administrator.");
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToAdd = request.Roles.Except(currentRoles).ToList();
        var rolesToRemove = currentRoles.Except(request.Roles).ToList();

        if (rolesToAdd.Count > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                return Jwt2FaResult<object>.BadRequest(addResult.Errors);
            }
        }

        if (rolesToRemove.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                return Jwt2FaResult<object>.BadRequest(removeResult.Errors);
            }
        }

        await PopulateRolesIfAwareAsync(user);

        return Jwt2FaResult<object>.Ok(_userProjector(user));
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<ResponseDto>> AdminUnlockUserAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return Jwt2FaResult<ResponseDto>.NotFound($"No user found with id '{id}'.");
        }

        var result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow);

        return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto
        {
            IsSuccess = result.Succeeded,
            Message = result.Succeeded ? null : string.Join(" ", result.Errors.Select(e => e.Description))
        });
    }

    /// <summary>
    /// The account-setup email sent both when an admin first creates an account and when
    /// ForgotPasswordAsync reissues a first-login link for one that's never set its own
    /// password — kept as a single call site so the two stay in sync.
    /// </summary>
    private Task SendAccountSetupEmailAsync(string email, string appName, string callbackUrl)
    {
        return _emailSender.SendEmailAsync(
            email,
            MessageTemplateFormatter.FormatHtml(_authCoreOptions.Value.AccountSetupEmailSubject, (ApplicationNameTokenName, appName)),
            MessageTemplateFormatter.FormatHtml(_authCoreOptions.Value.AccountSetupEmailBody,
                (ApplicationNameTokenName, appName), ("link", callbackUrl)));
    }

    /// <summary>
    /// The email-confirmation email sent both by a fresh self-registration and by a
    /// consumer-triggered resend — identical content either way, kept as a single call
    /// site so the two stay in sync.
    /// </summary>
    private Task SendEmailConfirmationEmailAsync(string email, string appName, string callbackUrl)
    {
        return _emailSender.SendEmailAsync(
            email,
            MessageTemplateFormatter.FormatHtml(_authCoreOptions.Value.EmailConfirmationEmailSubject, (ApplicationNameTokenName, appName)),
            MessageTemplateFormatter.FormatHtml(_authCoreOptions.Value.EmailConfirmationEmailBody,
                (ApplicationNameTokenName, appName), ("link", callbackUrl)));
    }

    private async Task PopulateRolesIfAwareAsync(TUser user)
    {
        if (user is IRoleAwareUser roleAware)
        {
            roleAware.Roles = (await _userManager.GetRolesAsync(user)).ToList();
        }
    }

    private static string GenerateTemporaryPassword()
    {
        // Discarded immediately in favor of the emailed first-login reset link — just
        // needs to satisfy whatever password policy the consumer configured.
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*";
        return RandomNumberGenerator.GetString(chars, TemporaryPasswordLength);
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
