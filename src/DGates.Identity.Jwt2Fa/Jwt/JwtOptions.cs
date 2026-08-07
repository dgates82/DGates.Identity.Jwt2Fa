namespace DGates.Identity.Jwt2Fa.Jwt;

/// <summary>Configuration for <see cref="IJwtTokenService{TUser}"/>, bound by <c>AddAuthCore</c>.</summary>
public class JwtOptions
{
    /// <summary>The configuration section name this options class binds to.</summary>
    public const string ConfigSection = "Jwt2FaConfig";

    /// <summary>The symmetric key used to sign and validate JWTs.</summary>
    public required string SecurityKey { get; set; }

    /// <summary>The expected "iss" (issuer) claim value.</summary>
    public required string ValidIssuer { get; set; }

    /// <summary>The expected "aud" (audience) claim value.</summary>
    public required string ValidAudience { get; set; }

    /// <summary>How many minutes after issuance a token remains valid.</summary>
    public double ExpiryInMinutes { get; set; } = 60;

    /// <summary>
    /// The Identity role name treated as "admin" for the self-or-admin authorization
    /// checks used across the account-activation, admin-provisioning, and 2FA modules
    /// (e.g. looking up or modifying another user's account). Configurable because a
    /// generic package can't assume every consumer names their admin role "Admin".
    /// </summary>
    public string AdminRoleName { get; set; } = "Admin";
}
