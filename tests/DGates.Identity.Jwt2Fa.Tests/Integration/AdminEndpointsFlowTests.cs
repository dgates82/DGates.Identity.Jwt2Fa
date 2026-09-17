using DGates.Identity.Jwt2Fa.Dtos;
using System.Net;
using System.Net.Http.Json;

namespace DGates.Identity.Jwt2Fa.Tests.Integration;

public class AdminEndpointsFlowTests : IntegrationTestBase
{
    private const string AdminEmail = "admin@example.com";
    private const string OtherEmail = "not-admin@example.com";
    private const string Password = "P@ssw0rd";

    private static readonly string[] SupportRole = ["Support"];

    [Fact]
    public async Task AdminEndpoints_ForNonAdminCaller_ReturnForbidden()
    {
        var token = await RegisterConfirmAndLoginAsync(OtherEmail, Password);

        var listResponse = await Client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/auth/listusers", token));
        var getByIdResponse = await Client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/auth/getuserbyid/whatever", token));
        var createResponse = await Client.SendAsync(WithJsonBody(
            AuthorizedRequest(HttpMethod.Post, "/auth/admincreateuser", token),
            new AdminCreateUserRequestDto { Email = "someone-else@example.com" }));

        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, getByIdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task PromotingAUserToAdmin_RequiresAFreshLogin_ForTheRoleClaimToTakeEffect()
    {
        var staleToken = await RegisterConfirmAndLoginAsync(AdminEmail, Password);
        await AddToRoleAsync(AdminEmail, "Admin");

        var staleResponse = await Client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/auth/listusers", staleToken));
        Assert.Equal(HttpStatusCode.Forbidden, staleResponse.StatusCode);

        var freshToken = await LoginAsync(AdminEmail, Password);
        var freshResponse = await Client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/auth/listusers", freshToken));
        Assert.True(freshResponse.IsSuccessStatusCode);
    }

    [Fact]
    public async Task AdminCreateUser_CreatesAccountAndSendsFirstLoginEmail_InOneCall()
    {
        var adminToken = await RegisterConfirmAndLoginAsync(AdminEmail, Password);
        await AddToRoleAsync(AdminEmail, "Admin");
        adminToken = await LoginAsync(AdminEmail, Password);

        const string newUserEmail = "new-hire@example.com";
        var createResponse = await Client.SendAsync(WithJsonBody(
            AuthorizedRequest(HttpMethod.Post, "/auth/admincreateuser", adminToken),
            new AdminCreateUserRequestDto { Email = newUserEmail }));

        createResponse.EnsureSuccessStatusCode();

        var welcomeEmail = EmailSender.SentEmails.Single(e => e.Email == newUserEmail);
        Assert.Contains("isFirstLogin=true", welcomeEmail.HtmlMessage);
        var passwordResetCode = EmailParsingHelper.ExtractQueryParam(welcomeEmail.HtmlMessage, "passwordResetCode");
        var userIdInLink = EmailParsingHelper.ExtractQueryParam(welcomeEmail.HtmlMessage, "userId");

        // The new user can use the bundled reset code to set their own password without ever knowing the generated one.
        var resetResponse = await Client.PostAsJsonAsync("/auth/resetpassword", new ResetPasswordRequestDto
        {
            UserId = userIdInLink,
            Password = "N3wP@ssw0rd",
            Code = passwordResetCode
        });
        var resetResult = await resetResponse.Content.ReadFromJsonAsync<ResponseDto>();
        Assert.True(resetResult!.IsSuccess);
        Assert.NotEmpty(userIdInLink);
    }

    [Fact]
    public async Task ListUsersAndGetUserById_AsAdmin_Succeed()
    {
        await RegisterConfirmAndLoginAsync(OtherEmail, Password);
        var otherUserId = EmailParsingHelper.ExtractQueryParam(
            EmailSender.SentEmails.Single(e => e.Email == OtherEmail).HtmlMessage, "userId");

        await RegisterConfirmAndLoginAsync(AdminEmail, Password);
        await AddToRoleAsync(AdminEmail, "Admin");
        var adminToken = await LoginAsync(AdminEmail, Password);

        var listResponse = await Client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/auth/listusers?page=1&pageSize=10", adminToken));
        listResponse.EnsureSuccessStatusCode();
        var page = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<object>>();
        Assert.Equal(2, page!.TotalCount);

        var getByIdResponse = await Client.SendAsync(
            AuthorizedRequest(HttpMethod.Get, $"/auth/getuserbyid/{otherUserId}", adminToken));
        getByIdResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ActivateAndDeactivate_ForNonAdminCaller_ReturnForbidden()
    {
        var token = await RegisterConfirmAndLoginAsync(OtherEmail, Password);

        var deactivateResponse = await Client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/auth/deactivate/whatever", token));
        var activateResponse = await Client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/auth/activate/whatever", token));

        Assert.Equal(HttpStatusCode.Forbidden, deactivateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, activateResponse.StatusCode);
    }

    [Fact]
    public async Task Deactivate_ThenAttemptLogin_IsRejectedByTheActivationPolicy()
    {
        await RegisterConfirmAndLoginAsync(OtherEmail, Password);
        var otherUserId = EmailParsingHelper.ExtractQueryParam(
            EmailSender.SentEmails.Single(e => e.Email == OtherEmail).HtmlMessage, "userId");

        await RegisterConfirmAndLoginAsync(AdminEmail, Password);
        await AddToRoleAsync(AdminEmail, "Admin");
        var adminToken = await LoginAsync(AdminEmail, Password);

        var deactivateResponse = await Client.SendAsync(
            AuthorizedRequest(HttpMethod.Post, $"/auth/deactivate/{otherUserId}", adminToken));
        deactivateResponse.EnsureSuccessStatusCode();

        var loginResponse = await Client.PostAsJsonAsync("/auth/login", new AuthRequestDto { Email = OtherEmail, Password = Password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.False(loginResult!.IsAuthSuccessful);

        var reactivateResponse = await Client.SendAsync(
            AuthorizedRequest(HttpMethod.Post, $"/auth/activate/{otherUserId}", adminToken));
        reactivateResponse.EnsureSuccessStatusCode();

        var secondLoginResponse = await Client.PostAsJsonAsync("/auth/login", new AuthRequestDto { Email = OtherEmail, Password = Password });
        var secondLoginResult = await secondLoginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.True(secondLoginResult!.IsAuthSuccessful);
    }

    [Fact]
    public async Task AdminUpdateUser_ForNonAdminCaller_ReturnsForbidden()
    {
        var token = await RegisterConfirmAndLoginAsync(OtherEmail, Password);

        var updateResponse = await Client.SendAsync(WithJsonBody(
            AuthorizedRequest(HttpMethod.Put, "/auth/adminupdateuser/whatever", token),
            new AdminUpdateUserRequestDto { Email = "irrelevant@example.com" }));

        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
    }

    [Fact]
    public async Task AdminUpdateUser_AsAdmin_UpdatesEmailAndDiffsRoles()
    {
        await RegisterConfirmAndLoginAsync(OtherEmail, Password);
        var otherUserId = EmailParsingHelper.ExtractQueryParam(
            EmailSender.SentEmails.Single(e => e.Email == OtherEmail).HtmlMessage, "userId");
        await AddToRoleAsync(OtherEmail, "Legacy");
        await EnsureRoleExistsAsync("Support");

        await RegisterConfirmAndLoginAsync(AdminEmail, Password);
        await AddToRoleAsync(AdminEmail, "Admin");
        var adminToken = await LoginAsync(AdminEmail, Password);

        const string newEmail = "renamed@example.com";
        var updateResponse = await Client.SendAsync(WithJsonBody(
            AuthorizedRequest(HttpMethod.Put, $"/auth/adminupdateuser/{otherUserId}", adminToken),
            new AdminUpdateUserRequestDto { Email = newEmail, Roles = SupportRole }));
        updateResponse.EnsureSuccessStatusCode();

        // Changing email resets EmailConfirmed, so the endpoint re-sends a confirmation
        // link to the new address — login can't succeed under RequireConfirmedAccount
        // until it's used.
        var confirmationEmail = EmailSender.SentEmails.Last(e => e.Email == newEmail);
        var confirmUserId = EmailParsingHelper.ExtractQueryParam(confirmationEmail.HtmlMessage, "userId");
        var confirmCode = EmailParsingHelper.ExtractQueryParam(confirmationEmail.HtmlMessage, "code");
        await Client.PostAsJsonAsync("/auth/confirmemail", new ConfirmEmailRequestDto { UserId = confirmUserId, Code = confirmCode });

        var loginResponse = await Client.PostAsJsonAsync("/auth/login", new AuthRequestDto { Email = newEmail, Password = Password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.True(loginResult!.IsAuthSuccessful);
    }

    [Fact]
    public async Task Unlock_ForNonAdminCaller_ReturnsForbidden()
    {
        var token = await RegisterConfirmAndLoginAsync(OtherEmail, Password);

        var unlockResponse = await Client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/auth/unlock/whatever", token));

        Assert.Equal(HttpStatusCode.Forbidden, unlockResponse.StatusCode);
    }

    [Fact]
    public async Task Unlock_AsAdmin_ClearsAnExistingLockout()
    {
        await RegisterConfirmAndLoginAsync(OtherEmail, Password);
        var otherUserId = EmailParsingHelper.ExtractQueryParam(
            EmailSender.SentEmails.Single(e => e.Email == OtherEmail).HtmlMessage, "userId");
        await LockOutUserAsync(OtherEmail);
        Assert.True(await IsLockedOutAsync(OtherEmail));

        await RegisterConfirmAndLoginAsync(AdminEmail, Password);
        await AddToRoleAsync(AdminEmail, "Admin");
        var adminToken = await LoginAsync(AdminEmail, Password);

        var unlockResponse = await Client.SendAsync(
            AuthorizedRequest(HttpMethod.Post, $"/auth/unlock/{otherUserId}", adminToken));
        unlockResponse.EnsureSuccessStatusCode();

        Assert.False(await IsLockedOutAsync(OtherEmail));
    }
}
