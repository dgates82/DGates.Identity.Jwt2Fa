using DGates.Identity.Jwt2Fa.Dtos;
using System.Net.Http.Json;

namespace DGates.Identity.Jwt2Fa.Tests.Integration;

public class AuthenticatorTwoFactorFlowTests : IntegrationTestBase
{
    private const string Email = "authenticator-user@example.com";
    private const string Password = "P@ssw0rd";

    [Fact]
    public async Task EnrollAuthenticatorAndCompleteTwoFactorLogin_Succeeds()
    {
        var token = await RegisterConfirmAndLoginAsync(Email, Password);

        var enableResponse = await Client.SendAsync(WithJsonBody(
            AuthorizedRequest(HttpMethod.Post, "/auth/enableauthenticator", token),
            new EnableAuthenticatorRequestDto { Email = Email }));
        var enableResult = await enableResponse.Content.ReadFromJsonAsync<EnableAuthenticatorResponseDto>();
        Assert.False(string.IsNullOrEmpty(enableResult!.SharedKey));

        var totpCode = TotpCodeGenerator.GenerateCode(enableResult.SharedKey);

        var verifyResponse = await Client.SendAsync(WithJsonBody(
            AuthorizedRequest(HttpMethod.Post, "/auth/verifyauthenticator", token),
            new VerifyAuthenticatorRequestDto { Email = Email, Method = "Authenticator", Code = totpCode }));
        var verifyResult = await verifyResponse.Content.ReadFromJsonAsync<VerifyAuthenticatorResponseDto>();
        Assert.True(verifyResult!.IsVerified);
        Assert.NotEmpty(verifyResult.Codes!);

        var loginResponse = await Client.PostAsJsonAsync("/auth/login", new AuthRequestDto { Email = Email, Password = Password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.False(loginResult!.IsAuthSuccessful);
        Assert.True(loginResult.RequiresTwoFactor);
        Assert.Equal("Authenticator", loginResult.TwoFactorMethod);

        var secondTotpCode = TotpCodeGenerator.GenerateCode(enableResult.SharedKey);
        var login2FaResponse = await Client.PostAsJsonAsync("/auth/login2fa", new TwoFaAuthRequestDto
        {
            Email = Email,
            TwoFactorProvider = "Authenticator",
            TwoFactorCode = secondTotpCode
        });
        var login2FaResult = await login2FaResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.True(login2FaResult!.IsAuthSuccessful);
        Assert.False(string.IsNullOrEmpty(login2FaResult.Token));

        var secureResponse = await Client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/auth/secure", login2FaResult.Token!));
        secureResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task VerifyAuthenticator_WithWrongCode_ReturnsNotVerified()
    {
        var token = await RegisterConfirmAndLoginAsync(Email, Password);

        var verifyResponse = await Client.SendAsync(WithJsonBody(
            AuthorizedRequest(HttpMethod.Post, "/auth/verifyauthenticator", token),
            new VerifyAuthenticatorRequestDto { Email = Email, Method = "Authenticator", Code = "000000" }));
        var verifyResult = await verifyResponse.Content.ReadFromJsonAsync<VerifyAuthenticatorResponseDto>();

        Assert.False(verifyResult!.IsVerified);
    }
}
