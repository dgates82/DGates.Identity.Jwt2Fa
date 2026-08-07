using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace DGates.Identity.Jwt2Fa.Jwt;

/// <inheritdoc cref="IJwtTokenService{TUser}" />
public sealed class JwtTokenService<TUser> : IJwtTokenService<TUser>
    where TUser : IdentityUser
{
    private readonly IOptions<JwtOptions> _options;
    private readonly Jwt2FaUserProjector<TUser> _userProjector;

    /// <summary>Creates the service with its bound options and registered user projector.</summary>
    public JwtTokenService(IOptions<JwtOptions> options, Jwt2FaUserProjector<TUser> userProjector)
    {
        _options = options;
        _userProjector = userProjector;
    }

    /// <inheritdoc />
    public SigningCredentials GetSigningCredentials()
    {
        var key = Encoding.UTF8.GetBytes(_options.Value.SecurityKey);
        var secret = new SymmetricSecurityKey(key);

        return new SigningCredentials(secret, SecurityAlgorithms.HmacSha256);
    }

    /// <inheritdoc />
    public IList<Claim> GetClaims(TUser user, IList<string> roles)
    {
        var projectedUser = _userProjector(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Email ?? user.UserName ?? user.Id),
            new("user", JsonSerializer.Serialize(projectedUser, projectedUser.GetType()), JsonClaimValueTypes.Json)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return claims;
    }

    /// <inheritdoc />
    public JwtSecurityToken GenerateToken(SigningCredentials signingCredentials, IList<Claim> claims)
    {
        return new JwtSecurityToken(
            issuer: _options.Value.ValidIssuer,
            audience: _options.Value.ValidAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.Value.ExpiryInMinutes),
            signingCredentials: signingCredentials);
    }

    /// <inheritdoc />
    public string WriteToken(JwtSecurityToken token) => new JwtSecurityTokenHandler().WriteToken(token);
}
