namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the 2FA-completion login endpoint.</summary>
public class TwoFaAuthRequestDto
{
    /// <summary>The email address completing a two-factor login.</summary>
    public required string Email { get; set; }

    /// <summary>The 2FA method being used: "Authenticator", "Email", "Phone", or "Sms".</summary>
    public required string TwoFactorProvider { get; set; }

    /// <summary>The 2FA code submitted for verification.</summary>
    public required string TwoFactorCode { get; set; }
}
