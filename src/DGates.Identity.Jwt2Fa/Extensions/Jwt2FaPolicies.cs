namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>Authorization policy names registered by this package.</summary>
public static class Jwt2FaPolicies
{
    /// <summary>
    /// Requires the caller to hold the configured <c>Jwt2FaConfig:AdminRoleName</c> role.
    /// Registered by <c>AddAuthCore</c>, since <c>AdminRoleName</c> is only known once
    /// configuration has been read there. Namespaced (not just <c>"AdminOnly"</c>) so it
    /// doesn't collide with a policy name the consuming app may already have.
    /// </summary>
    public const string AdminOnly = "Jwt2FaAdminOnly";
}
