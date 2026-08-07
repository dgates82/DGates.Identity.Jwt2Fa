namespace DGates.Identity.Jwt2Fa.Capabilities;

/// <summary>
/// Marks a user type with a simple boolean active flag. Backs the default
/// <see cref="PropertyBackedActivationPolicy{TUser}"/> so the common case (a plain
/// "is this account active" column) stays a one-liner, and the admin
/// activate/deactivate endpoints <c>AddDefaultActivationPolicy</c>/<c>MapDefaultActivationPolicy</c>
/// register. Consumers whose activation logic is more than a single flag should implement
/// <see cref="IActivationPolicy{TUser}"/> directly instead of this interface — they won't
/// get the built-in admin endpoints, since the package can't know how to toggle
/// arbitrary activation logic, and should add their own.
/// </summary>
public interface IActivatableUser
{
    /// <summary>Whether the user's account is active. Inactive users cannot log in.</summary>
    bool IsActive { get; set; }
}
