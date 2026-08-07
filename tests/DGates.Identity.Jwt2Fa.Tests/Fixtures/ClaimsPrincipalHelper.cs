using System.Security.Claims;

namespace DGates.Identity.Jwt2Fa.Tests.Fixtures;

/// <summary>
/// Builds a <see cref="ClaimsPrincipal"/> carrying only a <see cref="ClaimTypes.NameIdentifier"/>
/// claim — enough for the real (unmocked) <c>UserManager.GetUserAsync(ClaimsPrincipal)</c> to
/// resolve a caller via a mocked <c>FindByIdAsync</c>, without needing to mock <c>GetUserAsync</c> itself.
/// </summary>
public static class ClaimsPrincipalHelper
{
    public static ClaimsPrincipal ForUserId(string userId) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth"));
}
