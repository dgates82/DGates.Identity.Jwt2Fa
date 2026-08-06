namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>Configuration for the <c>AddAdminProvisioning</c>/<c>MapAdminProvisioning</c> module.</summary>
public class AdminProvisioningOptions
{
    /// <summary>The configuration section name this options class binds to.</summary>
    public const string ConfigSection = "Jwt2FaAdminProvisioningConfig";

    /// <summary>
    /// The absolute URL of the consuming app's password-reset page, with a <c>{code}</c>
    /// placeholder token the forgot-password endpoint substitutes before emailing it —
    /// e.g. <c>"https://myapp.example.com/forgot-password/reset?code={code}"</c>.
    /// </summary>
    public required string ForgotPasswordCallbackUrl { get; set; }
}
