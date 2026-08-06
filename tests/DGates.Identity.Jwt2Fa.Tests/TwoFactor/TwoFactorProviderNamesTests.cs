using DGates.Identity.Jwt2Fa.TwoFactor;

namespace DGates.Identity.Jwt2Fa.Tests.TwoFactor;

public class TwoFactorProviderNamesTests
{
    [Theory]
    [InlineData("Authenticator", "Authenticator")]
    [InlineData("Email", "Email")]
    [InlineData("Phone", "Phone")]
    [InlineData("Sms", "Phone")]
    public void Resolve_WithKnownMethod_ReturnsExpectedProvider(string method, string expected)
    {
        var result = TwoFactorProviderNames.Resolve(method);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("authenticator")]
    [InlineData("email")]
    [InlineData("Unknown")]
    public void Resolve_WithUnknownMethod_ReturnsNull(string method)
    {
        var result = TwoFactorProviderNames.Resolve(method);

        Assert.Null(result);
    }
}
