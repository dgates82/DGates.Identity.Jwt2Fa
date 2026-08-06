namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Response for the enable-authenticator endpoint.</summary>
public class EnableAuthenticatorResponseDto
{
    /// <summary>The authenticator key, formatted (space-separated, lowercase) for a user to type in manually.</summary>
    public required string SharedKey { get; set; }

    /// <summary>The <c>otpauth://</c> URI for the client to render as a QR code.</summary>
    public required string AuthenticatorUri { get; set; }
}
