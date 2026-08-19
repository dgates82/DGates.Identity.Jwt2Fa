using System.Text.Encodings.Web;

namespace DGates.Identity.Jwt2Fa.Services;

/// <summary>
/// Substitutes <c>{token}</c> placeholders in a consumer-configurable email/SMS
/// template (see <c>AuthCoreOptions</c>'s <c>*Subject</c>/<c>*Body</c> properties).
/// Shared by <see cref="AuthCoreService{TUser}"/> and <see cref="TwoFactorService{TUser}"/>
/// so every message is substituted the same way.
/// </summary>
internal static class MessageTemplateFormatter
{
    /// <summary>
    /// Formats an HTML email subject/body template. Every substituted value is
    /// HTML-encoded, including <c>{link}</c> — the same encoding the source template
    /// always applied to its callback URLs (query strings routinely contain <c>&amp;</c>,
    /// which is invalid unescaped inside an HTML attribute).
    /// </summary>
    public static string FormatHtml(string template, params (string Token, string Value)[] substitutions)
    {
        var result = template;
        foreach (var (token, value) in substitutions)
        {
            result = result.Replace($"{{{token}}}", HtmlEncoder.Default.Encode(value));
        }
        return result;
    }

    /// <summary>Formats a plain-text SMS template — no HTML encoding, substituted values go in as-is.</summary>
    public static string FormatPlainText(string template, params (string Token, string Value)[] substitutions)
    {
        var result = template;
        foreach (var (token, value) in substitutions)
        {
            result = result.Replace($"{{{token}}}", value);
        }
        return result;
    }
}
