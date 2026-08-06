using DGates.Identity.Jwt2Fa.Dtos;
using Microsoft.AspNetCore.Identity;

namespace DGates.Identity.Jwt2Fa.Services;

/// <summary>Core auth logic: register, login, and 2FA-completion login.</summary>
public interface IAuthCoreService<TUser>
    where TUser : IdentityUser
{
    /// <summary>Creates a new account and emails a confirmation link.</summary>
    Task<Jwt2FaResult<ResponseDto>> RegisterAsync(RegisterRequestDto request);

    /// <summary>Validates credentials and, if 2FA isn't required, issues a JWT.</summary>
    Task<Jwt2FaResult<AuthResponseDto>> LoginAsync(AuthRequestDto request);

    /// <summary>Completes login for a user who still needs to supply a second factor.</summary>
    Task<Jwt2FaResult<AuthResponseDto>> LoginTwoFactorAsync(TwoFaAuthRequestDto request);
}
