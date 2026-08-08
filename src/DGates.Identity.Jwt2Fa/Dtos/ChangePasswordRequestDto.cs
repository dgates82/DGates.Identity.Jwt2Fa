using System.ComponentModel.DataAnnotations;

namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the change-password endpoint.</summary>
public class ChangePasswordRequestDto
{
    /// <summary>The email address of the account changing its password.</summary>
    [Required, EmailAddress]
    public required string Email { get; set; }

    /// <summary>The user's current password, required to authorize the change.</summary>
    [Required]
    public required string CurrentPassword { get; set; }

    /// <summary>The new password to set.</summary>
    [Required]
    public required string NewPassword { get; set; }
}
