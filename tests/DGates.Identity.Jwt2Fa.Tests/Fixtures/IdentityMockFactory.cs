using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;

namespace DGates.Identity.Jwt2Fa.Tests.Fixtures;

/// <summary>
/// Builds mockable <see cref="UserManager{TUser}"/>/<see cref="SignInManager{TUser}"/>
/// instances. Both classes are concrete (not interfaces), but Microsoft designed their
/// public members as <c>virtual</c> specifically so <c>Mock&lt;T&gt;</c> can override
/// individual methods directly — this is the standard ASP.NET Core Identity test pattern,
/// not a workaround. <c>CallBase = true</c> so unconfigured methods (e.g.
/// <c>GetUserAsync(ClaimsPrincipal)</c>) fall through to the real implementation — which
/// in turn calls other virtual methods (e.g. <c>FindByIdAsync</c>) that tests configure
/// directly, instead of every caller-resolution path needing its own explicit setup.
/// </summary>
public static class IdentityMockFactory
{
    /// <summary>Creates a <see cref="Mock{T}"/> of <see cref="UserManager{TUser}"/> with a no-op backing store.</summary>
    public static Mock<UserManager<TUser>> CreateUserManagerMock<TUser>()
        where TUser : class
    {
        var store = new Mock<IUserStore<TUser>>();
        var identityOptions = Options.Create(new IdentityOptions());
        var mock = new Mock<UserManager<TUser>>(store.Object, identityOptions, null!, null!, null!, null!, null!, null!, null!)
        {
            CallBase = true
        };
        return mock;
    }

    /// <summary>Creates a <see cref="Mock{T}"/> of <see cref="SignInManager{TUser}"/> wrapping <paramref name="userManager"/>.</summary>
    public static Mock<SignInManager<TUser>> CreateSignInManagerMock<TUser>(UserManager<TUser> userManager)
        where TUser : class
    {
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<TUser>>();
        var mock = new Mock<SignInManager<TUser>>(
            userManager, contextAccessor.Object, claimsFactory.Object, null!, null!, null!, null!)
        {
            CallBase = true
        };
        return mock;
    }
}
