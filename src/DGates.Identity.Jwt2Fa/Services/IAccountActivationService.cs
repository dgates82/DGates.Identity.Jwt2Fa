using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace DGates.Identity.Jwt2Fa.Services;

/// <summary>Account-activation-adjacent logic: self-or-admin-gated user lookup.</summary>
public interface IAccountActivationService<TUser>
    where TUser : IdentityUser
{
    /// <summary>
    /// Looks up a user by email. <paramref name="caller"/> must be the target user or
    /// hold the configured admin role.
    /// </summary>
    Task<Jwt2FaResult<object>> GetUserByEmailAsync(string email, ClaimsPrincipal caller);
}
