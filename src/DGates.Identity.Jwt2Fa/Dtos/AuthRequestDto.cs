namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the login endpoint.</summary>
public class AuthRequestDto
{
    /// <summary>The email address to authenticate.</summary>
    public required string Email { get; set; }

    /// <summary>The account's password.</summary>
    public required string Password { get; set; }
}
