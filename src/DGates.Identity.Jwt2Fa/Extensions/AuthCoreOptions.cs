namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>Configuration for the <c>AddAuthCore</c>/<c>MapAuthCore</c> module.</summary>
public class AuthCoreOptions
{
    /// <summary>The configuration section name this options class binds to.</summary>
    public const string ConfigSection = "Jwt2FaAuthCoreConfig";

    /// <summary>
    /// The application name used in the registration confirmation email's subject and
    /// body. Replaces the source template's hardcoded <c>[Application Name]</c> placeholder.
    /// </summary>
    public required string ApplicationName { get; set; }

    /// <summary>
    /// The absolute URL of the consuming app's email-confirmation page, with
    /// <c>{userId}</c> and <c>{code}</c> placeholder tokens the register endpoint
    /// substitutes before emailing it — e.g.
    /// <c>"https://myapp.example.com/email-confirmation?userId={userId}&amp;emailCode={code}"</c>.
    /// Replaces the source template's hardcoded <c>/email-confirmation</c> same-origin
    /// route, which assumed a single-process API+SPA deployment.
    /// </summary>
    public required string EmailConfirmationCallbackUrl { get; set; }
}
