using System.ComponentModel.DataAnnotations;

namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the reset-password endpoint.</summary>
public class ResetPasswordRequestDto
{
    /// <summary>The id of the account resetting its password, carried on the reset link.</summary>
    [Required]
    public required string UserId { get; set; }

    /// <summary>The new password to set.</summary>
    [Required]
    public required string Password { get; set; }

    /// <summary>The password reset code emailed to the user via the forgot-password endpoint.</summary>
    [Required]
    public required string Code { get; set; }
}
