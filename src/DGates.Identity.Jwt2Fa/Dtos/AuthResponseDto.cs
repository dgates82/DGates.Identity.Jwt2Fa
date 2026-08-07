namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Response for the login and 2FA-completion endpoints.</summary>
public class AuthResponseDto
{
    /// <summary>Whether authentication completed successfully and a <see cref="Token"/> was issued.</summary>
    public bool IsAuthSuccessful { get; set; }

    /// <summary>A human-readable error message when authentication failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>The signed JWT to use for subsequent authenticated requests, present only on success.</summary>
    public string? Token { get; set; }

    /// <summary>
    /// Whether a second factor is still required. When <c>true</c> alongside
    /// <see cref="IsAuthSuccessful"/> <c>= false</c>, the client should prompt for a
    /// 2FA code and call the 2FA-completion endpoint.
    /// </summary>
    public bool RequiresTwoFactor { get; set; }

    /// <summary>The user's configured 2FA method: "Authenticator", "Email", or "Phone".</summary>
    public string? TwoFactorMethod { get; set; }

    /// <summary>The user's phone number, populated only when <see cref="TwoFactorMethod"/> is "Phone".</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// The authenticated user, populated only on success, shaped by whichever
    /// <c>Jwt2FaUserProjector&lt;TUser&gt;</c> the consumer registered.
    /// </summary>
    public object? User { get; set; }
}
