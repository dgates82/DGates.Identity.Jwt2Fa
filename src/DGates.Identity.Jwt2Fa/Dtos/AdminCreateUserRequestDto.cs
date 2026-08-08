using System.ComponentModel.DataAnnotations;

namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>
/// Request for the admin-create-user endpoint. Deliberately its own DTO, not a bind
/// directly to <c>TUser</c> — binding a request body straight to the user entity risks
/// mass assignment (a caller setting fields like an id or an audit timestamp that
/// were never meant to be attacker-controlled).
/// </summary>
public class AdminCreateUserRequestDto
{
    /// <summary>The new account's email address, also used as the username.</summary>
    [Required, EmailAddress]
    public required string Email { get; set; }

    /// <summary>Roles to assign the new user, if any.</summary>
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
