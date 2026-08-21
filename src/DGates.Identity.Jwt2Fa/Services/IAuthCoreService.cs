using DGates.Identity.Jwt2Fa.Dtos;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace DGates.Identity.Jwt2Fa.Services;

/// <summary>
/// Core auth logic: register, login, the password/email lifecycle (forgot/reset/change
/// password, email confirmation), and account lookup. These need nothing beyond
/// <see cref="IdentityUser"/> to work, so they aren't gated behind any capability
/// interface — see <see cref="Capabilities.IAdminProvisionableUser"/> for the one place
/// a capability opportunistically enhances behavior here (<see cref="ResetPasswordAsync"/>
/// writes it if present; <see cref="SendEmailConfirmationAsync"/> reads it if present).
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

    /// <summary>
    /// Changes a user's password given their current password. <paramref name="caller"/>
    /// must be the target user or hold the configured admin role.
    /// </summary>
    Task<Jwt2FaResult<ResponseDto>> ChangePasswordAsync(ChangePasswordRequestDto request, ClaimsPrincipal caller);

    /// <summary>Resends the email confirmation link for a user.</summary>
    Task<Jwt2FaResult<ResponseDto>> SendEmailConfirmationAsync(SendEmailConfirmationRequestDto request);

    /// <summary>Confirms a user's email using the code from the confirmation link.</summary>
    Task<Jwt2FaResult<ResponseDto>> ConfirmEmailAsync(ConfirmEmailRequestDto request);

    /// <summary>
    /// Looks up a user by email. <paramref name="caller"/> must be the target user or
    /// hold the configured admin role.
    /// </summary>
    Task<Jwt2FaResult<object>> GetUserByEmailAsync(string email, ClaimsPrincipal caller);

    /// <summary>Looks up a user by id. Admin-only — enforced by the endpoint's authorization policy, not this method.</summary>
    Task<Jwt2FaResult<object>> GetUserByIdAsync(string id);

    /// <summary>Lists users, paginated. Admin-only — enforced by the endpoint's authorization policy, not this method.</summary>
    Task<Jwt2FaResult<PagedResultDto<object>>> ListUsersAsync(int page, int pageSize);

    /// <summary>
    /// Creates a user with a generated password and emails the same bundled
    /// confirmation+first-login-password-reset link <see cref="SendEmailConfirmationAsync"/>
    /// sends for not-yet-set-password accounts — one atomic call instead of the caller
    /// needing to chain create-then-send-confirmation themselves. Admin-only — enforced
    /// by the endpoint's authorization policy, not this method.
    /// </summary>
    Task<Jwt2FaResult<object>> AdminCreateUserAsync(AdminCreateUserRequestDto request);

    /// <summary>
    /// Updates a user's email and role membership (added/removed via a full-set diff
    /// against their current roles). Deliberately doesn't touch anything beyond those two
    /// identity concerns — app-specific profile fields are the consuming app's own update
    /// endpoint's job. Changing the email re-sends a confirmation email, since Identity
    /// resets <c>EmailConfirmed</c> on any email change. Admin-only — enforced by the
    /// endpoint's authorization policy, not this method.
    /// </summary>
    Task<Jwt2FaResult<object>> AdminUpdateUserAsync(string id, AdminUpdateUserRequestDto request);

    /// <summary>
    /// Clears a user's lockout by setting <c>LockoutEnd</c> to now, so
    /// <c>UserManager.IsLockedOutAsync</c> no longer reports them locked. Doesn't reset
    /// <c>AccessFailedCount</c> — matches Identity's own lockout semantics, where that
    /// resets on the next successful sign-in, not on an explicit unlock. Requires nothing
    /// beyond <see cref="IdentityUser"/>; lockout is a base Identity concept, not a
    /// capability interface. Admin-only — enforced by the endpoint's authorization
    /// policy, not this method.
    /// </summary>
    Task<Jwt2FaResult<ResponseDto>> AdminUnlockUserAsync(string id);
}
