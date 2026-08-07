namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>
/// Request for the admin-update-user endpoint. Deliberately narrow, like
/// <see cref="AdminCreateUserRequestDto"/> — only the identity concerns this package
/// knows about (email, role membership). App-specific profile fields (name, address,
/// etc.) aren't package-known and stay on the consuming app's own update endpoint;
/// binding a request body straight to <c>TUser</c> risks mass assignment.
/// </summary>
public class AdminUpdateUserRequestDto
{
    /// <summary>The user's email address, also used as the username.</summary>
    public required string Email { get; set; }

    /// <summary>
    /// The user's complete role membership after this update — roles present here but
    /// not currently assigned are added, roles currently assigned but absent here are
    /// removed. Not a delta; always the full desired set.
    /// </summary>
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
