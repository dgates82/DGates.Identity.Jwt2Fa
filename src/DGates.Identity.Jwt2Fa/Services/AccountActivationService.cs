using DGates.Identity.Jwt2Fa.Dtos;
using DGates.Identity.Jwt2Fa.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace DGates.Identity.Jwt2Fa.Services;

/// <inheritdoc cref="IAccountActivationService{TUser}" />
public sealed class AccountActivationService<TUser> : IAccountActivationService<TUser>
    where TUser : IdentityUser
{
    private readonly UserManager<TUser> _userManager;
    private readonly IOptions<JwtOptions> _jwtOptions;
    private readonly Jwt2FaUserProjector<TUser> _userProjector;

    /// <summary>Creates the service with its user store and admin-role configuration.</summary>
    public AccountActivationService(
        UserManager<TUser> userManager,
        IOptions<JwtOptions> jwtOptions,
        Jwt2FaUserProjector<TUser> userProjector)
    {
        _userManager = userManager;
        _jwtOptions = jwtOptions;
        _userProjector = userProjector;
    }

    /// <inheritdoc />
    public async Task<Jwt2FaResult<object>> GetUserByEmailAsync(string email, ClaimsPrincipal caller)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Jwt2FaResult<object>.Ok(new ResponseDto { IsSuccess = false });
        }

        var currentUser = await _userManager.GetUserAsync(caller);
        var isSelf = currentUser is not null && currentUser.Id == user.Id;
        var isAdmin = currentUser is not null
            && await _userManager.IsInRoleAsync(currentUser, _jwtOptions.Value.AdminRoleName);

        if (!isSelf && !isAdmin)
        {
            return Jwt2FaResult<object>.BadRequest(
                "You can only look up your own account. Looking up another account requires the admin role.");
        }

        return Jwt2FaResult<object>.Ok(_userProjector(user));
    }
}
