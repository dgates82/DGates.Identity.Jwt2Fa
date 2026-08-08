using System.ComponentModel.DataAnnotations;

namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the confirm-email endpoint.</summary>
public class ConfirmEmailRequestDto
{
    /// <summary>The ID of the user confirming their email.</summary>
    [Required]
    public required string UserId { get; set; }

    /// <summary>The email confirmation code from the link sent via the register or resend-confirmation endpoints.</summary>
    [Required]
    public required string Code { get; set; }
}
