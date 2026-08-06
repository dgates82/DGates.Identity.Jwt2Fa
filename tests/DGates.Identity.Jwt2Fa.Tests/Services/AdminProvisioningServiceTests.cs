using DGates.Identity.Jwt2Fa.Dtos;
using DGates.Identity.Jwt2Fa.Extensions;
using DGates.Identity.Jwt2Fa.Services;
using DGates.Identity.Jwt2Fa.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace DGates.Identity.Jwt2Fa.Tests.Services;

public class AdminProvisioningServiceTests
{
    private const string Email = "user@example.com";

    private readonly Mock<UserManager<TestUser>> _userManager = IdentityMockFactory.CreateUserManagerMock<TestUser>();
    private readonly Mock<SignInManager<TestUser>> _signInManager;
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly IOptions<AuthCoreOptions> _authCoreOptions = Options.Create(new AuthCoreOptions
    {
        ApplicationName = "Test App",
        EmailConfirmationCallbackUrl = "https://app.example.com/email-confirmation?userId={userId}&code={code}"
    });
    private readonly IOptions<AdminProvisioningOptions> _provisioningOptions = Options.Create(new AdminProvisioningOptions
    {
        ForgotPasswordCallbackUrl = "https://app.example.com/forgot-password/reset?code={code}"
    });

    public AdminProvisioningServiceTests()
    {
        _signInManager = IdentityMockFactory.CreateSignInManagerMock(_userManager.Object);
    }

    private AdminProvisioningService<TestUser> CreateService() => new(
        _userManager.Object, _signInManager.Object, _emailSender.Object, _authCoreOptions, _provisioningOptions);

    [Fact]
    public async Task ForgotPasswordAsync_WithUnconfirmedEmail_ReturnsOkWithoutRevealingState()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.IsEmailConfirmedAsync(user)).ReturnsAsync(false);
        var service = CreateService();

        var result = await service.ForgotPasswordAsync(new ForgotPasswordDto { Email = Email });

        Assert.False(result.Value!.IsSuccess);
        _emailSender.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WithConfirmedEmail_SendsResetEmail()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.IsEmailConfirmedAsync(user)).ReturnsAsync(true);
        _userManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("raw-code");
        var service = CreateService();

        var result = await service.ForgotPasswordAsync(new ForgotPasswordDto { Email = Email });

        Assert.True(result.Value!.IsSuccess);
        _emailSender.Verify(x => x.SendEmailAsync(Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_OnSuccess_SetsHasSetPasswordTrue()
    {
        var user = new TestUser { Id = "1", Email = Email, HasSetPassword = false };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager
            .Setup(x => x.ResetPasswordAsync(user, It.IsAny<string>(), "NewP@ss1"))
            .ReturnsAsync(IdentityResult.Success);
        var service = CreateService();

        var result = await service.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Email = Email,
            Password = "NewP@ss1",
            Code = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")).TrimEnd('=').Replace('+', '-').Replace('/', '_')
        });

        Assert.True(result.Value!.IsSuccess);
        Assert.True(user.HasSetPassword);
        _userManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_OnFailure_DoesNotSetHasSetPassword()
    {
        var user = new TestUser { Id = "1", Email = Email, HasSetPassword = false };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager
            .Setup(x => x.ResetPasswordAsync(user, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Weak password." }));
        var service = CreateService();

        var result = await service.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Email = Email,
            Password = "weak",
            Code = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")).TrimEnd('=').Replace('+', '-').Replace('/', '_')
        });

        Assert.False(result.Value!.IsSuccess);
        Assert.False(user.HasSetPassword);
        _userManager.Verify(x => x.UpdateAsync(It.IsAny<TestUser>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_OnSuccess_RefreshesSignIn()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager
            .Setup(x => x.ChangePasswordAsync(user, "old", "new"))
            .ReturnsAsync(IdentityResult.Success);
        _signInManager.Setup(x => x.RefreshSignInAsync(user)).Returns(Task.CompletedTask);
        var service = CreateService();

        var result = await service.ChangePasswordAsync(new ChangePasswordRequestDto
        {
            Email = Email,
            CurrentPassword = "old",
            NewPassword = "new"
        });

        Assert.True(result.Value!.IsSuccess);
        _signInManager.Verify(x => x.RefreshSignInAsync(user), Times.Once);
    }

    [Fact]
    public async Task SendEmailConfirmationAsync_WhenHasNotSetPassword_AppendsFirstLoginParams()
    {
        var user = new TestUser { Id = "1", Email = Email, HasSetPassword = false };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(user)).ReturnsAsync("email-code");
        _userManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-code");
        string? capturedBody = null;
        _emailSender
            .Setup(x => x.SendEmailAsync(Email, It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, body) => capturedBody = body)
            .Returns(Task.CompletedTask);
        var service = CreateService();

        await service.SendEmailConfirmationAsync(new SendEmailConfirmationRequestDto { Email = Email });

        Assert.Contains("isFirstLogin=true", capturedBody);
        Assert.Contains("passwordResetCode=", capturedBody);
    }

    [Fact]
    public async Task SendEmailConfirmationAsync_WhenHasSetPassword_OmitsFirstLoginParams()
    {
        var user = new TestUser { Id = "1", Email = Email, HasSetPassword = true };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(user)).ReturnsAsync("email-code");
        string? capturedBody = null;
        _emailSender
            .Setup(x => x.SendEmailAsync(Email, It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, body) => capturedBody = body)
            .Returns(Task.CompletedTask);
        var service = CreateService();

        await service.SendEmailConfirmationAsync(new SendEmailConfirmationRequestDto { Email = Email });

        Assert.DoesNotContain("isFirstLogin", capturedBody);
        _userManager.Verify(x => x.GeneratePasswordResetTokenAsync(It.IsAny<TestUser>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmEmailAsync_ReturnsIdentityResultOutcome()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.ConfirmEmailAsync(user, It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        var service = CreateService();

        var result = await service.ConfirmEmailAsync(new ConfirmEmailRequestDto
        {
            UserId = "1",
            Code = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")).TrimEnd('=').Replace('+', '-').Replace('/', '_')
        });

        Assert.True(result.Value!.IsSuccess);
    }
}
