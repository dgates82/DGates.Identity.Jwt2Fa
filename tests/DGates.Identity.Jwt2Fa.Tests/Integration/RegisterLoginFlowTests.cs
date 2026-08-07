using DGates.Identity.Jwt2Fa.Dtos;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace DGates.Identity.Jwt2Fa.Tests.Integration;

public class RegisterLoginFlowTests : IntegrationTestBase
{
    private const string Email = "integration-user@example.com";
    private const string Password = "P@ssw0rd";

    [Fact]
    public async Task RegisterConfirmLoginAndCallProtectedEndpoint_Succeeds()
    {
        var registerResponse = await Client.PostAsJsonAsync("/auth/register", new RegisterRequestDto
        {
            Email = Email,
            Password = Password
        });
        registerResponse.EnsureSuccessStatusCode();
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<ResponseDto>();
        Assert.True(registerResult!.IsSuccess);

        var confirmationEmail = Assert.Single(EmailSender.SentEmails);
        var userId = EmailParsingHelper.ExtractQueryParam(confirmationEmail.HtmlMessage, "userId");
        var code = EmailParsingHelper.ExtractQueryParam(confirmationEmail.HtmlMessage, "code");
        var emailParam = EmailParsingHelper.ExtractQueryParam(confirmationEmail.HtmlMessage, "email");
        Assert.Equal(Email, emailParam);

        var confirmResponse = await Client.PostAsJsonAsync("/auth/confirmEmail", new ConfirmEmailRequestDto
        {
            UserId = userId,
            Code = code
        });
        var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ResponseDto>();
        Assert.True(confirmResult!.IsSuccess);

        var loginResponse = await Client.PostAsJsonAsync("/auth/login", new AuthRequestDto
        {
            Email = Email,
            Password = Password
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.True(loginResult!.IsAuthSuccessful);
        Assert.False(string.IsNullOrEmpty(loginResult.Token));

        var secureRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/secure");
        secureRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.Token);
        var secureResponse = await Client.SendAsync(secureRequest);

        secureResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Login_WithoutConfirmingEmail_Fails()
    {
        await Client.PostAsJsonAsync("/auth/register", new RegisterRequestDto { Email = Email, Password = Password });

        var loginResponse = await Client.PostAsJsonAsync("/auth/login", new AuthRequestDto
        {
            Email = Email,
            Password = Password
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.False(loginResult!.IsAuthSuccessful);
    }

    [Fact]
    public async Task CallProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/auth/secure");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
