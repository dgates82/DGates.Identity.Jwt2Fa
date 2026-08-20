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
        var resetUserId = EmailParsingHelper.ExtractQueryParam(resetEmail.HtmlMessage, "userId");

        var resetResponse = await Client.PostAsJsonAsync("/auth/resetpassword", new ResetPasswordRequestDto
        {
            UserId = resetUserId,
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

    [Fact]
    public async Task ForgotPassword_ForAdminCreatedAccountThatNeverSetAPassword_SendsTheAccountSetupEmail()
    {
        const string adminEmail = "forgot-password-admin@example.com";
        const string newUserEmail = "forgot-password-new-hire@example.com";
        const string password = "P@ssw0rd";

        var adminToken = await RegisterConfirmAndLoginAsync(adminEmail, password);
        await AddToRoleAsync(adminEmail, "Admin");
        adminToken = await LoginAsync(adminEmail, password);

        await Client.SendAsync(WithJsonBody(
            AuthorizedRequest(HttpMethod.Post, "/auth/admincreateuser", adminToken),
            new AdminCreateUserRequestDto { Email = newUserEmail }));

        // Confirm the email, then abandon it without setting a password - the "stuck" state a reissued forgotpassword link exists for.
        var welcomeEmail = EmailSender.SentEmails.Single(e => e.Email == newUserEmail);
        var userId = EmailParsingHelper.ExtractQueryParam(welcomeEmail.HtmlMessage, "userId");
        var confirmCode = EmailParsingHelper.ExtractQueryParam(welcomeEmail.HtmlMessage, "code");
        await Client.PostAsJsonAsync("/auth/confirmemail", new ConfirmEmailRequestDto { UserId = userId, Code = confirmCode });

        var forgotResponse = await Client.PostAsJsonAsync("/auth/forgotpassword", new ForgotPasswordDto { Email = newUserEmail });
        var forgotResult = await forgotResponse.Content.ReadFromJsonAsync<ResponseDto>();
        Assert.True(forgotResult!.IsSuccess);

        var resentEmail = EmailSender.SentEmails.Last(e => e.Email == newUserEmail);
        Assert.Equal("Test App Account Created", resentEmail.Subject);
        Assert.Contains("An account has been created for you", resentEmail.HtmlMessage);
        Assert.DoesNotContain("Forgot your password", resentEmail.HtmlMessage);
        Assert.Contains("isFirstLogin=true", resentEmail.HtmlMessage);

        // And the reissued link's code is a genuinely working reset token, not just correctly worded.
        var resetCode = EmailParsingHelper.ExtractQueryParam(resentEmail.HtmlMessage, "code");
        var resetUserId = EmailParsingHelper.ExtractQueryParam(resentEmail.HtmlMessage, "userId");
        var resetResponse = await Client.PostAsJsonAsync("/auth/resetpassword", new ResetPasswordRequestDto
        {
            UserId = resetUserId,
            Password = "N3wP@ssw0rd",
            Code = resetCode
        });
        var resetResult = await resetResponse.Content.ReadFromJsonAsync<ResponseDto>();
        Assert.True(resetResult!.IsSuccess);

        var loginResponse = await Client.PostAsJsonAsync("/auth/login", new AuthRequestDto { Email = newUserEmail, Password = "N3wP@ssw0rd" });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.True(loginResult!.IsAuthSuccessful);
    }
}
