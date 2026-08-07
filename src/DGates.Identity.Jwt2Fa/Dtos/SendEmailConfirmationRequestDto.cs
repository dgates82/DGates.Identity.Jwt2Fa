namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the resend-email-confirmation endpoint.</summary>
public class SendEmailConfirmationRequestDto
{
    /// <summary>The email address to resend a confirmation link to.</summary>
    public required string Email { get; set; }
}
