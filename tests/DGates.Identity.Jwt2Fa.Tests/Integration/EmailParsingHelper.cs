using System.Text.RegularExpressions;

namespace DGates.Identity.Jwt2Fa.Tests.Integration;

/// <summary>Pulls codes/links back out of the HTML bodies <see cref="FakeEmailSender"/> captures.</summary>
public static partial class EmailParsingHelper
{
    public static string ExtractQueryParam(string htmlBody, string paramName)
    {
        var match = Regex.Match(htmlBody, $@"[?&](?:amp;)?{Regex.Escape(paramName)}=([^&'""]+)");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Query param '{paramName}' not found in: {htmlBody}");
        }
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    public static string ExtractTwoFaCode(string htmlBody)
    {
        var match = TwoFaCodeRegex().Match(htmlBody);
        if (!match.Success)
        {
            throw new InvalidOperationException($"2FA code not found in: {htmlBody}");
        }
        return match.Groups[1].Value;
    }

    [GeneratedRegex(@"code is: (\d+)")]
    private static partial Regex TwoFaCodeRegex();
}
