namespace DGates.Identity.Jwt2Fa.Capabilities;

/// <summary>
/// Decides whether a user is allowed to log in. A policy service rather than a
/// property on <c>TUser</c>, so it can express more than a single flag (active AND
/// email-confirmed AND not soft-deleted AND grace period not expired, or a call out
/// to a billing service) — see <see cref="PropertyBackedActivationPolicy{TUser}"/> for
/// the simple, single-flag case.
/// </summary>
/// <typeparam name="TUser">The consumer's Identity user type.</typeparam>
public interface IActivationPolicy<in TUser>
{
    /// <summary>Whether <paramref name="user"/> is currently allowed to log in.</summary>
    bool IsActive(TUser user);
}
