using DGates.Identity.Jwt2Fa.Jwt;
using DGates.Identity.Jwt2Fa.Tests.Fixtures;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DGates.Identity.Jwt2Fa.Tests.Jwt;

public class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        SecurityKey = "this-is-a-sufficiently-long-test-signing-key",
        ValidIssuer = "test-issuer",
        ValidAudience = "test-audience",
        ExpiryInMinutes = 30
    };

    private static JwtTokenService<TestUser> CreateService(Jwt2FaUserProjector<TestUser>? projector = null) =>
        new(Microsoft.Extensions.Options.Options.Create(Options), projector ?? (user => new { user.Id, user.Email }));

    [Fact]
    public void GetSigningCredentials_UsesConfiguredKeyAndHmacSha256()
    {
        var service = CreateService();

        var credentials = service.GetSigningCredentials();

        Assert.Equal(SecurityAlgorithms.HmacSha256, credentials.Algorithm);
        var key = Assert.IsType<SymmetricSecurityKey>(credentials.Key);
        Assert.Equal(Encoding.UTF8.GetBytes(Options.SecurityKey), key.Key);
    }

    [Fact]
    public void GetClaims_IncludesNameIdentifierNameAndRoleClaims()
    {
        var service = CreateService();
        var user = new TestUser { Id = "user-1", Email = "user@example.com", UserName = "user@example.com" };

        var claims = service.GetClaims(user, new[] { "Admin", "Support" });

        Assert.Contains(claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == "user-1");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == "user@example.com");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Support");
    }

    [Fact]
    public void GetClaims_EmbedsProjectedUserAsJsonClaim()
    {
        var service = CreateService(user => new { user.Id });
        var user = new TestUser { Id = "user-1", Email = "user@example.com" };

        var claims = service.GetClaims(user, Array.Empty<string>());

        var userClaim = Assert.Single(claims, c => c.Type == "user");
        Assert.Equal(JsonClaimValueTypes.Json, userClaim.ValueType);
        Assert.Contains("\"Id\":\"user-1\"", userClaim.Value);
    }

    [Fact]
    public void GetClaims_FallsBackToUserNameThenId_WhenEmailIsNull()
    {
        var service = CreateService();
        var user = new TestUser { Id = "user-1", Email = null, UserName = "fallback-username" };

        var claims = service.GetClaims(user, Array.Empty<string>());

        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == "fallback-username");
    }

    [Fact]
    public void GenerateToken_SetsIssuerAudienceAndExpiry()
    {
        var service = CreateService();
        var credentials = service.GetSigningCredentials();
        var beforeCall = DateTime.UtcNow;

        var token = service.GenerateToken(credentials, new List<Claim>());

        Assert.Equal(Options.ValidIssuer, token.Issuer);
        Assert.Equal(Options.ValidAudience, token.Audiences.Single());
        Assert.True(token.ValidTo > beforeCall.AddMinutes(Options.ExpiryInMinutes - 1));
        Assert.True(token.ValidTo <= beforeCall.AddMinutes(Options.ExpiryInMinutes + 1));
    }

    [Fact]
    public void WriteToken_ProducesAParsableJwtWithTheOriginalClaims()
    {
        var service = CreateService();
        var user = new TestUser { Id = "user-1", Email = "user@example.com" };
        var claims = service.GetClaims(user, new[] { "Admin" });
        var token = service.GenerateToken(service.GetSigningCredentials(), claims);

        var jwt = service.WriteToken(token);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        Assert.Contains(parsed.Claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == "user-1");
        Assert.Contains(parsed.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }
}
