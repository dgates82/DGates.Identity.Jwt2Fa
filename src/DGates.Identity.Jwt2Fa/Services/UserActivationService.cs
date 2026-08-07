using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Dtos;
using Microsoft.AspNetCore.Identity;

namespace DGates.Identity.Jwt2Fa.Services;

/// <inheritdoc cref="IUserActivationService{TUser}" />
public sealed class UserActivationService<TUser> : IUserActivationService<TUser>
    where TUser : IdentityUser, IActivatableUser
{
    private readonly UserManager<TUser> _userManager;

    public UserActivationService(UserManager<TUser> userManager)
    {
        _userManager = userManager;
    }

    /// <inheritdoc />
    public Task<Jwt2FaResult<ResponseDto>> ActivateAsync(string id) => SetActiveAsync(id, true);

    /// <inheritdoc />
    public Task<Jwt2FaResult<ResponseDto>> DeactivateAsync(string id) => SetActiveAsync(id, false);

    private async Task<Jwt2FaResult<ResponseDto>> SetActiveAsync(string id, bool isActive)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return Jwt2FaResult<ResponseDto>.NotFound($"No user found with id '{id}'.");
        }

        user.IsActive = isActive;
        var result = await _userManager.UpdateAsync(user);

        return Jwt2FaResult<ResponseDto>.Ok(new ResponseDto
        {
            IsSuccess = result.Succeeded,
            Message = result.Succeeded ? null : string.Join(" ", result.Errors.Select(e => e.Description))
        });
    }
}
