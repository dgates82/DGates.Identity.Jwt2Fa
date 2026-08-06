namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the forgot-password endpoint.</summary>
public class ForgotPasswordDto
{
    /// <summary>The email address of the account requesting a password reset.</summary>
    public required string Email { get; set; }
}
