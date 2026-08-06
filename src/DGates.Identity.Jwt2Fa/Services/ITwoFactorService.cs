using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Dtos;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace DGates.Identity.Jwt2Fa.Services;

/// <summary>2FA enrollment/delivery logic: send-code, enable/verify/reset authenticator.</summary>
public interface ITwoFactorService<TUser>
    where TUser : IdentityUser, IMultiFactorMethodUser
{
    /// <summary>Generates and delivers a 2FA code via email or SMS.</summary>
    Task<Jwt2FaResult<ResponseDto>> SendTwoFaCodeAsync(SendVerificationCodeRequestDto request, ClaimsPrincipal caller);

    /// <summary>Generates (or reuses) an authenticator app key and its QR-code enrollment URI.</summary>
    Task<Jwt2FaResult<object>> EnableAuthenticatorAsync(EnableAuthenticatorRequestDto request, ClaimsPrincipal caller);

    /// <summary>Verifies a 2FA code and, on success, enables 2FA for the user with the given method.</summary>
    Task<Jwt2FaResult<object>> VerifyAuthenticatorAsync(VerifyAuthenticatorRequestDto request, ClaimsPrincipal caller);

    /// <summary>Disables 2FA and clears the authenticator key, requiring re-enrollment from scratch.</summary>
    Task<Jwt2FaResult<ResponseDto>> ResetAuthenticatorAsync(EnableAuthenticatorRequestDto request, ClaimsPrincipal caller);
}
