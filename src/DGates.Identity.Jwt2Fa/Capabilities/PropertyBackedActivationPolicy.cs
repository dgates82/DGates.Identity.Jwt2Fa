namespace DGates.Identity.Jwt2Fa.Capabilities;

/// <summary>
/// Default <see cref="IActivationPolicy{TUser}"/> for the common case: activation is
/// just a single boolean column. Registered automatically by <c>AddAccountActivation</c>
/// when <c>TUser</c> implements <see cref="IActivatableUser"/>; consumers with more
/// than a single flag's worth of activation logic should register their own
/// <see cref="IActivationPolicy{TUser}"/> instead.
/// </summary>
public sealed class PropertyBackedActivationPolicy<TUser> : IActivationPolicy<TUser>
    where TUser : IActivatableUser
{
    /// <inheritdoc />
    public bool IsActive(TUser user) => user.IsActive;
}
