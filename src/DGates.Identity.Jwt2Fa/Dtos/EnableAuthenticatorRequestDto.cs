namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Request for the enable-authenticator and reset-authenticator endpoints.</summary>
public class EnableAuthenticatorRequestDto
{
    /// <summary>The email address of the account enabling or resetting authenticator-app 2FA.</summary>
    public required string Email { get; set; }
}
