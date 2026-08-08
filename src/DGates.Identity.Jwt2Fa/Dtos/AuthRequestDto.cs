using System.ComponentModel.DataAnnotations;

namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the login endpoint.</summary>
public class AuthRequestDto
{
    /// <summary>The email address to authenticate.</summary>
    [Required, EmailAddress]
    public required string Email { get; set; }

    /// <summary>The account's password.</summary>
    [Required]
    public required string Password { get; set; }
}
