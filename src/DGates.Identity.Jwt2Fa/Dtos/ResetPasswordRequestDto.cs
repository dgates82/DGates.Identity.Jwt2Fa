using System.ComponentModel.DataAnnotations;

namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the reset-password endpoint.</summary>
public class ResetPasswordRequestDto
{
    /// <summary>
    /// The id of the account resetting its password — carried on the reset link
    /// (<c>ForgotPasswordPath</c>'s <c>{userId}</c> token) rather than asked for on
    /// the form; the reset code is already cryptographically bound to a specific
    /// user, so nothing is gained by also requiring the client to submit (and a form
    /// to display/edit) an email address.
    /// </summary>
    [Required]
    public required string UserId { get; set; }

    /// <summary>The new password to set.</summary>
    [Required]
    public required string Password { get; set; }

    /// <summary>The password reset code emailed to the user via the forgot-password endpoint.</summary>
    [Required]
    public required string Code { get; set; }
}
