namespace DGates.Identity.Jwt2Fa.Capabilities;

/// <summary>
/// Marks a user type with a simple boolean active flag. Backs the default
/// <see cref="PropertyBackedActivationPolicy{TUser}"/> so the common case (a plain
/// "is this account active" column) stays a one-liner. Consumers whose activation
/// logic is more than a single flag should implement <see cref="IActivationPolicy{TUser}"/>
/// directly instead of this interface.
/// </summary>
public interface IActivatableUser
{
    /// <summary>Whether the user's account is active. Inactive users cannot log in.</summary>
    bool IsActive { get; }
}
