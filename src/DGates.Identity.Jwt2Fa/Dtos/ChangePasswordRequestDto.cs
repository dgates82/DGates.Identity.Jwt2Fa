namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the change-password endpoint.</summary>
public class ChangePasswordRequestDto
{
    /// <summary>The email address of the account changing its password.</summary>
    public required string Email { get; set; }

    /// <summary>The user's current password, required to authorize the change.</summary>
    public required string CurrentPassword { get; set; }

    /// <summary>The new password to set.</summary>
    public required string NewPassword { get; set; }
}
