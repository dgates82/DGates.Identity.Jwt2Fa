using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;

namespace DGates.Identity.Jwt2Fa.TwoFactor;

/// <summary>Formats a raw TOTP authenticator key for display and QR-code enrollment.</summary>
public static class AuthenticatorKeyFormatter
{
    // otpauth:// is the fixed scheme the authenticator-app QR standard requires, not a configurable path.
#pragma warning disable S1075
    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";
#pragma warning restore S1075

    /// <summary>Splits an authenticator key into space-separated 4-character groups, lowercased for readability.</summary>
    public static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Builds the <c>otpauth://totp/...</c> QR-code URI for a user's authenticator key,
    /// using <paramref name="applicationName"/> as the issuer.
    /// </summary>
    public static string BuildAuthenticatorUri(string applicationName, string email, string unformattedKey)
    {
        var urlEncoder = UrlEncoder.Default;

        return string.Format(
            CultureInfo.InvariantCulture,
            AuthenticatorUriFormat,
            urlEncoder.Encode(applicationName),
            urlEncoder.Encode(email),
            unformattedKey);
    }
}
