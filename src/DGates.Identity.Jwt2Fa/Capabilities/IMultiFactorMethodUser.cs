namespace DGates.Identity.Jwt2Fa.Capabilities;

/// <summary>
/// Marks a user type that tracks which 2FA delivery method it has configured. Like
/// <see cref="IAdminProvisionableUser"/>, the 2FA enrollment flow writes this value
/// (not just reads it for a decision), so it's a capability interface, not a policy.
/// </summary>
public interface IMultiFactorMethodUser
{
    /// <summary>
    /// The configured 2FA method: <c>"Authenticator"</c>, <c>"Email"</c>, or <c>"Phone"</c>;
    /// <c>null</c> if 2FA isn't enabled.
    /// </summary>
    string? TwoFactorMethod { get; set; }
}
