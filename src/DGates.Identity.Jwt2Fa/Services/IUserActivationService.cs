using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Dtos;
using Microsoft.AspNetCore.Identity;

namespace DGates.Identity.Jwt2Fa.Services;

/// <summary>
/// Admin actions for toggling <see cref="IActivatableUser.IsActive"/>. Only registered for
/// <typeparamref name="TUser"/> that implement <see cref="IActivatableUser"/> — the
/// single-flag case <see cref="PropertyBackedActivationPolicy{TUser}"/> already backs.
/// Consumers with a custom <see cref="IActivationPolicy{TUser}"/> (e.g. a billing-driven
/// check with no single flag to toggle) own their own admin action instead.
/// </summary>
public interface IUserActivationService<TUser>
    where TUser : IdentityUser, IActivatableUser
{
    /// <summary>Sets <see cref="IActivatableUser.IsActive"/> to <c>true</c> for the given user id.</summary>
    Task<Jwt2FaResult<ResponseDto>> ActivateAsync(string id);

    /// <summary>Sets <see cref="IActivatableUser.IsActive"/> to <c>false</c> for the given user id.</summary>
    Task<Jwt2FaResult<ResponseDto>> DeactivateAsync(string id);
}
