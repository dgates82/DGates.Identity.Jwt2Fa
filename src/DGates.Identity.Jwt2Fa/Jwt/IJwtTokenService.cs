using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace DGates.Identity.Jwt2Fa.Jwt;

/// <summary>Builds the signing credentials, claims, and signed JWT issued on successful login.</summary>
/// <typeparam name="TUser">The consumer's Identity user type.</typeparam>
public interface IJwtTokenService<TUser>
    where TUser : IdentityUser
{
    /// <summary>Builds the HMAC-SHA256 signing credentials from the configured security key.</summary>
    SigningCredentials GetSigningCredentials();

    /// <summary>
    /// Builds the claims for a token: standard name/role claims (so framework
    /// authorization and <c>UserManager.GetUserAsync</c> work without a database
    /// round-trip), plus the registered <see cref="Jwt2FaUserProjector{TUser}"/>'s
    /// projection serialized as a JSON claim.
    /// </summary>
    IList<Claim> GetClaims(TUser user, IList<string> roles);

    /// <summary>Builds the signed JWT with the configured issuer, audience, and expiry.</summary>
    JwtSecurityToken GenerateToken(SigningCredentials signingCredentials, IList<Claim> claims);

    /// <summary>Serializes a <see cref="JwtSecurityToken"/> to its compact string form.</summary>
    string WriteToken(JwtSecurityToken token);
}
