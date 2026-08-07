namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>Configuration for the <c>AddAuthCore</c>/<c>MapAuthCore</c> module.</summary>
public class AuthCoreOptions
{
    /// <summary>The configuration section name this options class binds to.</summary>
    public const string ConfigSection = "Jwt2FaAuthCoreConfig";

    /// <summary>
    /// The application name used in emails (registration confirmation, password reset)
    /// and the authenticator QR-code issuer. Replaces the source template's hardcoded
    /// <c>[Application Name]</c> placeholder and hardcoded issuer string.
    /// </summary>
    public required string ApplicationName { get; set; }

    /// <summary>
    /// The consuming app's frontend origin, e.g. <c>"https://myapp.example.com"</c> — no
    /// trailing slash. Combined with <see cref="EmailConfirmationPath"/>/
    /// <see cref="ForgotPasswordPath"/> to build the links emailed to users, so the
    /// domain only needs to be configured once.
    /// </summary>
    public required string FrontendBaseUrl { get; set; }

    /// <summary>
    /// The path (plus query string) of the consuming app's email-confirmation page,
    /// appended to <see cref="FrontendBaseUrl"/>, with <c>{userId}</c>, <c>{code}</c>,
    /// and <c>{email}</c> placeholder tokens substituted before emailing it — e.g.
    /// <c>"/email-confirmation?userId={userId}&amp;email={email}&amp;code={code}"</c>.
    /// <c>{email}</c> is substituted consistently everywhere this template is used
    /// (register, admincreateuser, sendemailconfirmation, and adminupdateuser's
    /// re-confirmation-on-email-change), so your confirmation page can use the address
    /// without an extra lookup; leave the token out of the template if you don't need
    /// it, it's simply never substituted. Deliberately just a template, not a fixed
    /// route this package assumes — the source template's hardcoded
    /// <c>/email-confirmation</c> same-origin route assumed a single-process API+SPA
    /// deployment, which this replaces.
    /// </summary>
    public required string EmailConfirmationPath { get; set; }

    /// <summary>
    /// The path (plus query string) of the consuming app's password-reset page,
    /// appended to <see cref="FrontendBaseUrl"/>, with a <c>{code}</c> placeholder token
    /// substituted before emailing it — e.g. <c>"/forgot-password/reset?code={code}"</c>.
    /// </summary>
    public required string ForgotPasswordPath { get; set; }

    /// <summary>
    /// The upper bound <c>listusers</c> clamps its <c>pageSize</c> query parameter to,
    /// regardless of what the caller requests — an unbounded "give me everyone" page size
    /// would defeat the point of paginating an admin endpoint as the user table grows.
    /// Defaults to 100; raise it if a smaller table genuinely needs bigger pages.
    /// </summary>
    public int MaxPageSize { get; set; } = 100;
}
