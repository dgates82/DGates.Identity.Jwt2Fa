using System.ComponentModel.DataAnnotations;

namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the register endpoint.</summary>
public class RegisterRequestDto
{
    /// <summary>The new account's email address, also used as the username.</summary>
    [Required, EmailAddress]
    public required string Email { get; set; }

    /// <summary>The new account's password.</summary>
    [Required]
    public required string Password { get; set; }
}
