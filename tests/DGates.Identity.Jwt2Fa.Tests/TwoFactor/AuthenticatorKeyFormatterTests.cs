using DGates.Identity.Jwt2Fa.TwoFactor;

namespace DGates.Identity.Jwt2Fa.Tests.TwoFactor;

public class AuthenticatorKeyFormatterTests
{
    [Theory]
    [InlineData("ABCDEFGHIJKLMNOP", "abcd efgh ijkl mnop")]
    [InlineData("ABCDEFGHIJ", "abcd efgh ij")]
    [InlineData("AB", "ab")]
    [InlineData("", "")]
    [InlineData("ABCD", "abcd")]
    public void FormatKey_GroupsIntoLowercaseFourCharBlocks(string unformattedKey, string expected)
    {
        var result = AuthenticatorKeyFormatter.FormatKey(unformattedKey);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildAuthenticatorUri_ProducesExpectedOtpauthUri()
    {
        var uri = AuthenticatorKeyFormatter.BuildAuthenticatorUri("My App", "user@example.com", "SECRETKEY");

        Assert.Equal(
            "otpauth://totp/My%20App:user@example.com?secret=SECRETKEY&issuer=My%20App&digits=6",
            uri);
    }
}
