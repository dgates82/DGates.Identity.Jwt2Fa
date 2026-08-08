using System.ComponentModel.DataAnnotations;

namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the reset-password endpoint.</summary>
public class ResetPasswordRequestDto
{
    /// <summary>The email address of the account resetting its password.</summary>
    [Required, EmailAddress]
    public required string Email { get; set; }

    /// <summary>The new password to set.</summary>
    [Required]
    public required string Password { get; set; }

    /// <summary>The password reset code emailed to the user via the forgot-password endpoint.</summary>
    [Required]
    public required string Code { get; set; }
}
