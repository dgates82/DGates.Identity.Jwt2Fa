namespace DGates.Identity.Jwt2Fa.Capabilities;

/// <summary>
/// Marks a user type that tracks whether the account holder has ever set their own
/// password, as opposed to still using one an admin assigned at creation time. Unlike
/// <see cref="IActivatableUser"/>, this is state the package actively writes (first-login
/// branching sets it), not just a yes/no gate it reads — so it's a capability interface,
/// not a policy.
/// </summary>
public interface IAdminProvisionableUser
{
    /// <summary>Whether the user has set their own password.</summary>
    bool HasSetPassword { get; set; }
}
