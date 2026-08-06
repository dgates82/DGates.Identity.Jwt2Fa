namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Response for the verify-authenticator endpoint.</summary>
public class VerifyAuthenticatorResponseDto
{
    /// <summary>Whether the submitted 2FA code was valid.</summary>
    public bool IsVerified { get; set; }

    /// <summary>A human-readable message describing the outcome.</summary>
    public required string Message { get; set; }

    /// <summary>
    /// Freshly generated recovery codes, populated only when 2FA was verified using
    /// the authenticator app method.
    /// </summary>
    public string[]? Codes { get; set; }
}
