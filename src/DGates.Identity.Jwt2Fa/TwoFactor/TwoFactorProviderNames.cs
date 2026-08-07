namespace DGates.Identity.Jwt2Fa.TwoFactor;

/// <summary>
/// Maps the client-facing 2FA method name used in request DTOs to the ASP.NET Core
/// Identity token provider name registered by <c>AddDefaultTokenProviders</c>. Shared
/// between the core login2fa endpoint and the <c>Add2Fa</c> module's endpoints, since
/// both need to resolve the same client string to the same Identity provider.
/// </summary>
public static class TwoFactorProviderNames
{
    /// <summary>
    /// Resolves <paramref name="method"/> ("Authenticator", "Email", "Phone", or "Sms" —
    /// "Phone" and "Sms" are aliases for the same Identity "Phone" provider) to its
    /// Identity token provider name, or <c>null</c> if unrecognized.
    /// </summary>
    public static string? Resolve(string method) => method switch
    {
        "Authenticator" => "Authenticator",
        "Email" => "Email",
        "Phone" or "Sms" => "Phone",
        _ => null
    };
}
