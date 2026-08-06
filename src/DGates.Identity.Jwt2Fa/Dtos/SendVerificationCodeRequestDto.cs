namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the 2FA code-send endpoint.</summary>
public class SendVerificationCodeRequestDto
{
    /// <summary>The email address to send a 2FA code to.</summary>
    public required string Email { get; set; }

    /// <summary>The phone number to send an SMS code to, if <see cref="Method"/> is "Phone" and 2FA isn't already enabled.</summary>
    public string PhoneNumber { get; set; } = "";

    /// <summary>The delivery method: "Email" or "Phone" ("Authenticator" is invalid here, since those codes are generated client-side).</summary>
    public required string Method { get; set; }
}
