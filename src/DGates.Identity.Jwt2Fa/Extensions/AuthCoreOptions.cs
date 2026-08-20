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
    /// appended to <see cref="FrontendBaseUrl"/>, with <c>{userId}</c> and <c>{code}</c>
    /// placeholder tokens substituted before emailing it — e.g.
    /// <c>"/email-confirmation?userId={userId}&amp;code={code}"</c>. Deliberately just
    /// a template, not a fixed route this package assumes — the source template's
    /// hardcoded <c>/email-confirmation</c> same-origin route assumed a single-process
    /// API+SPA deployment, which this replaces.
    /// </summary>
    public required string EmailConfirmationPath { get; set; }

    /// <summary>
    /// The path (plus query string) of the consuming app's password-reset page,
    /// appended to <see cref="FrontendBaseUrl"/>, with <c>{userId}</c> and <c>{code}</c>
    /// placeholder tokens substituted before emailing it — e.g.
    /// <c>"/forgot-password/reset?userId={userId}&amp;code={code}"</c>. <c>{userId}</c>
    /// is how <c>resetpassword</c> identifies the account, not a submitted email
    /// address — the reset code is already cryptographically bound to a specific
    /// user, so your reset-password page needs nothing more than the new password.
    /// </summary>
    public required string ForgotPasswordPath { get; set; }

    /// <summary>
    /// The upper bound <c>listusers</c> clamps its <c>pageSize</c> query parameter to,
    /// regardless of what the caller requests — an unbounded "give me everyone" page size
    /// would defeat the point of paginating an admin endpoint as the user table grows.
    /// Defaults to 100; raise it if a smaller table genuinely needs bigger pages.
    /// </summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>Subject for the email confirmation link sent by <c>register</c>/<c>sendemailconfirmation</c>. Supports <c>{applicationName}</c>.</summary>
    public string EmailConfirmationEmailSubject { get; set; } = "{applicationName} Email Confirmation";

    /// <summary>Body for the email confirmation link sent by <c>register</c>/<c>sendemailconfirmation</c>. Supports <c>{applicationName}</c> and <c>{link}</c>.</summary>
    public string EmailConfirmationEmailBody { get; set; } =
        "In order to start using {applicationName}, you need to verify your email.<br/><br/>" +
        "Please confirm your account by <a href='{link}'>clicking here</a>.<br/><br/>" +
        "If you did not request a login to {applicationName}, please ignore this email.";

    /// <summary>
    /// Subject for the account-setup email sent by <c>admincreateuser</c>, and by
    /// <c>forgotpassword</c> when reissuing a first-login link for an account that's
    /// never set its own password. Supports <c>{applicationName}</c>.
    /// </summary>
    public string AccountSetupEmailSubject { get; set; } = "{applicationName} Account Created";

    /// <summary>
    /// Body for the account-setup email sent by <c>admincreateuser</c>, and by
    /// <c>forgotpassword</c> when reissuing a first-login link for an account that's
    /// never set its own password. Supports <c>{applicationName}</c> and <c>{link}</c>.
    /// </summary>
    public string AccountSetupEmailBody { get; set; } =
        "An account has been created for you on {applicationName}.<br/><br/>" +
        "Please confirm your account and set your password by <a href='{link}'>clicking here</a>.<br/><br/>" +
        "If you were not expecting this, please ignore this email.";

    /// <summary>Subject for <c>forgotpassword</c>'s reset link, for an account that has already set its own password. Supports <c>{applicationName}</c>.</summary>
    public string ForgotPasswordEmailSubject { get; set; } = "{applicationName} Password Reset";

    /// <summary>Body for <c>forgotpassword</c>'s reset link, for an account that has already set its own password. Supports <c>{applicationName}</c> and <c>{link}</c>.</summary>
    public string ForgotPasswordEmailBody { get; set; } =
        "Forgot your password?<br/><br/>We received a request to reset the password for your account.<br/><br/>" +
        "To reset your password <a href='{link}'>click here</a>.<br/><br/>" +
        "If you did not request a password reset please ignore this email.";

    /// <summary>Subject for a 2FA verification code sent by email. Supports <c>{applicationName}</c>.</summary>
    public string TwoFactorCodeEmailSubject { get; set; } = "{applicationName} 2FA Code";

    /// <summary>Body for a 2FA verification code sent by email. Supports <c>{code}</c>.</summary>
    public string TwoFactorCodeEmailBody { get; set; } =
        "Your 2FA code is: {code}<br/><br/>If you did not request a 2FA code please ignore this email.";

    /// <summary>Body for a 2FA verification code sent by SMS — no subject, plain text. Supports <c>{applicationName}</c> and <c>{code}</c>.</summary>
    public string TwoFactorCodeSmsBody { get; set; } =
        "Your 2FA code for {applicationName} is: {code}. DO NOT share it with anyone.";
}
