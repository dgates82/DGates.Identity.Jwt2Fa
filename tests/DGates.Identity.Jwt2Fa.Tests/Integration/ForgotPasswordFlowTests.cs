using DGates.Identity.Jwt2Fa.Dtos;
using System.Net.Http.Json;

namespace DGates.Identity.Jwt2Fa.Tests.Integration;

public class ForgotPasswordFlowTests : IntegrationTestBase
{
    private const string Email = "forgot-password-user@example.com";
    private const string OldPassword = "P@ssw0rd";
    private const string NewPassword = "N3wP@ssw0rd";

    [Fact]
    public async Task ForgotPasswordResetAndLogin_WithNewPassword_Succeeds()
    {
        await RegisterConfirmAndLoginAsync(Email, OldPassword);

        var forgotResponse = await Client.PostAsJsonAsync("/auth/forgotpassword", new ForgotPasswordDto { Email = Email });
        var forgotResult = await forgotResponse.Content.ReadFromJsonAsync<ResponseDto>();
        Assert.True(forgotResult!.IsSuccess);

        var resetEmail = EmailSender.SentEmails.Last(e => e.Email == Email);
        var resetCode = EmailParsingHelper.ExtractQueryParam(resetEmail.HtmlMessage, "code");

        var resetResponse = await Client.PostAsJsonAsync("/auth/resetpassword", new ResetPasswordRequestDto
        {
            Email = Email,
            Password = NewPassword,
            Code = resetCode
        });
        var resetResult = await resetResponse.Content.ReadFromJsonAsync<ResponseDto>();
        Assert.True(resetResult!.IsSuccess);

        var newLoginResponse = await Client.PostAsJsonAsync("/auth/login", new AuthRequestDto { Email = Email, Password = NewPassword });
        var newLoginResult = await newLoginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.True(newLoginResult!.IsAuthSuccessful);

        var oldLoginResponse = await Client.PostAsJsonAsync("/auth/login", new AuthRequestDto { Email = Email, Password = OldPassword });
        var oldLoginResult = await oldLoginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.False(oldLoginResult!.IsAuthSuccessful);
    }

    [Fact]
    public async Task ForgotPassword_ForUnknownEmail_ReturnsOkWithoutRevealingNonExistence()
    {
        var response = await Client.PostAsJsonAsync("/auth/forgotpassword", new ForgotPasswordDto { Email = "nobody@example.com" });
        var result = await response.Content.ReadFromJsonAsync<ResponseDto>();

        Assert.False(result!.IsSuccess);
        Assert.Empty(EmailSender.SentEmails);
    }
}
