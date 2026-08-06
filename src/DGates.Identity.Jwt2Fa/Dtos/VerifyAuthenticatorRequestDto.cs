namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the verify-authenticator endpoint.</summary>
public class VerifyAuthenticatorRequestDto
{
    /// <summary>The user's email address.</summary>
    public required string Email { get; set; }

    /// <summary>The phone number to enable for SMS-based 2FA, if <see cref="Method"/> is "Phone".</summary>
    public string PhoneNumber { get; set; } = "";

    /// <summary>The 2FA method being verified: "Authenticator", "Email", "Phone", or "Sms".</summary>
    public required string Method { get; set; }

    /// <summary>The 2FA code submitted for verification.</summary>
    public required string Code { get; set; }
}
