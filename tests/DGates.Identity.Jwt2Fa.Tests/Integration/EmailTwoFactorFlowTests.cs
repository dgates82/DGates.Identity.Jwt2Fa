using DGates.Identity.Jwt2Fa.Dtos;
using System.Net.Http.Json;

namespace DGates.Identity.Jwt2Fa.Tests.Integration;

public class EmailTwoFactorFlowTests : IntegrationTestBase
{
    private const string Email = "email-2fa-user@example.com";
    private const string Password = "P@ssw0rd";

    [Fact]
    public async Task EnrollEmailTwoFactorAndCompleteTwoFactorLogin_Succeeds()
    {
        var token = await RegisterConfirmAndLoginAsync(Email, Password);

        var sendResponse = await Client.SendAsync(WithJsonBody(
            AuthorizedRequest(HttpMethod.Post, "/auth/sendtwofacode", token),
            new SendVerificationCodeRequestDto { Email = Email, Method = "Email" }));
        var sendResult = await sendResponse.Content.ReadFromJsonAsync<ResponseDto>();
        Assert.True(sendResult!.IsSuccess);

        var enrollmentCode = EmailParsingHelper.ExtractTwoFaCode(
            EmailSender.SentEmails.Last(e => e.Email == Email).HtmlMessage);

        var verifyResponse = await Client.SendAsync(WithJsonBody(
            AuthorizedRequest(HttpMethod.Post, "/auth/verifyauthenticator", token),
            new VerifyAuthenticatorRequestDto { Email = Email, Method = "Email", Code = enrollmentCode }));
        var verifyResult = await verifyResponse.Content.ReadFromJsonAsync<VerifyAuthenticatorResponseDto>();
        Assert.True(verifyResult!.IsVerified);

        var loginResponse = await Client.PostAsJsonAsync("/auth/login", new AuthRequestDto { Email = Email, Password = Password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.True(loginResult!.RequiresTwoFactor);
        Assert.Equal("Email", loginResult.TwoFactorMethod);

        // Sending a fresh code for an already-enrolled user needs no bearer token — the caller isn't authenticated yet.
        await Client.PostAsJsonAsync("/auth/sendtwofacode", new SendVerificationCodeRequestDto { Email = Email, Method = "Email" });
        var loginCode = EmailParsingHelper.ExtractTwoFaCode(
            EmailSender.SentEmails.Last(e => e.Email == Email).HtmlMessage);

        var login2FaResponse = await Client.PostAsJsonAsync("/auth/login2fa", new TwoFaAuthRequestDto
        {
            Email = Email,
            TwoFactorProvider = "Email",
            TwoFactorCode = loginCode
        });
        var login2FaResult = await login2FaResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.True(login2FaResult!.IsAuthSuccessful);
        Assert.False(string.IsNullOrEmpty(login2FaResult.Token));
    }
}
