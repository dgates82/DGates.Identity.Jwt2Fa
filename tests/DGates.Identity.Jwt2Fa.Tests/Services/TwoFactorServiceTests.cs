using DGates.Identity.Jwt2Fa.Dtos;
using DGates.Identity.Jwt2Fa.Extensions;
using DGates.Identity.Jwt2Fa.Jwt;
using DGates.Identity.Jwt2Fa.Services;
using DGates.Identity.Jwt2Fa.Tests.Fixtures;
using DGates.Identity.NotificationProviders.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace DGates.Identity.Jwt2Fa.Tests.Services;

public class TwoFactorServiceTests
{
    private const string Email = "user@example.com";

    private readonly Mock<UserManager<TestUser>> _userManager = IdentityMockFactory.CreateUserManagerMock<TestUser>();
    private readonly Mock<SignInManager<TestUser>> _signInManager;
    private readonly Mock<IJwtTokenService<TestUser>> _jwtTokenService = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<ISmsSender> _smsSender = new();
    private readonly IOptions<AuthCoreOptions> _authCoreOptions = Options.Create(new AuthCoreOptions
    {
        ApplicationName = "Test App",
        FrontendBaseUrl = "https://app.example.com",
        EmailConfirmationPath = "/email-confirmation?userId={userId}&code={code}",
        ForgotPasswordPath = "/forgot-password/reset?code={code}"
    });
    private readonly IOptions<JwtOptions> _jwtOptions = Options.Create(new JwtOptions
    {
        SecurityKey = "test-key-test-key-test-key-1234",
        ValidIssuer = "issuer",
        ValidAudience = "audience",
        AdminRoleName = "Admin"
    });

    public TwoFactorServiceTests()
    {
        _signInManager = IdentityMockFactory.CreateSignInManagerMock(_userManager.Object);
    }

    private TwoFactorService<TestUser> CreateService() => new(
        _userManager.Object,
        _signInManager.Object,
        _jwtTokenService.Object,
        user => new { user.Id },
        _emailSender.Object,
        _smsSender.Object,
        _authCoreOptions,
        _jwtOptions);

    [Fact]
    public async Task LoginTwoFactorAsync_WithUnknownUser_ReturnsOk()
    {
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync((TestUser?)null);
        var service = CreateService();

        var result = await service.LoginTwoFactorAsync(new TwoFaAuthRequestDto
        {
            Email = Email,
            TwoFactorProvider = "Email",
            TwoFactorCode = "123456"
        });

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.False(result.Value!.IsAuthSuccessful);
    }

    [Fact]
    public async Task LoginTwoFactorAsync_WithUnknownProvider_ReturnsOkWithError()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        var service = CreateService();

        var result = await service.LoginTwoFactorAsync(new TwoFaAuthRequestDto
        {
            Email = Email,
            TwoFactorProvider = "NotARealProvider",
            TwoFactorCode = "123456"
        });

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.False(result.Value!.IsAuthSuccessful);
        Assert.Equal("Invalid Authentication Code", result.Value.ErrorMessage);
    }

    [Fact]
    public async Task LoginTwoFactorAsync_WithInvalidCode_ReturnsOkWithError()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.VerifyTwoFactorTokenAsync(user, "Email", "000000")).ReturnsAsync(false);
        var service = CreateService();

        var result = await service.LoginTwoFactorAsync(new TwoFaAuthRequestDto
        {
            Email = Email,
            TwoFactorProvider = "Email",
            TwoFactorCode = "000000"
        });

        Assert.False(result.Value!.IsAuthSuccessful);
        Assert.Equal("Invalid Authentication Code", result.Value.ErrorMessage);
    }

    [Fact]
    public async Task LoginTwoFactorAsync_WithValidCode_ReturnsTokenAndProjectedUser()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.VerifyTwoFactorTokenAsync(user, "Email", "123456")).ReturnsAsync(true);
        _userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        SetUpJwtTokenServiceToReturn("signed.jwt.token");
        var service = CreateService();

        var result = await service.LoginTwoFactorAsync(new TwoFaAuthRequestDto
        {
            Email = Email,
            TwoFactorProvider = "Email",
            TwoFactorCode = "123456"
        });

        Assert.True(result.Value!.IsAuthSuccessful);
        Assert.Equal("signed.jwt.token", result.Value.Token);
        Assert.True(result.Value.RequiresTwoFactor);
        Assert.NotNull(result.Value.User);
    }

    [Fact]
    public async Task LoginTwoFactorAsync_WithValidCode_PopulatesRolesBeforeProjecting()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.VerifyTwoFactorTokenAsync(user, "Email", "123456")).ReturnsAsync(true);
        _userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        SetUpJwtTokenServiceToReturn("signed.jwt.token");
        var service = CreateService();

        await service.LoginTwoFactorAsync(new TwoFaAuthRequestDto
        {
            Email = Email,
            TwoFactorProvider = "Email",
            TwoFactorCode = "123456"
        });

        Assert.Equal(new[] { "Admin" }, user.Roles);
    }

    private void SetUpJwtTokenServiceToReturn(string token)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(new byte[32]), SecurityAlgorithms.HmacSha256);
        _jwtTokenService.Setup(x => x.GetSigningCredentials()).Returns(credentials);
        _jwtTokenService
            .Setup(x => x.GetClaims(It.IsAny<TestUser>(), It.IsAny<IList<string>>()))
            .Returns(new List<Claim>());
        _jwtTokenService
            .Setup(x => x.GenerateToken(credentials, It.IsAny<IList<Claim>>()))
            .Returns(new JwtSecurityToken());
        _jwtTokenService.Setup(x => x.WriteToken(It.IsAny<JwtSecurityToken>())).Returns(token);
    }

    [Fact]
    public async Task SendTwoFaCodeAsync_ForEnrolledUser_SkipsAuthCheckAndSendsEmail()
    {
        var user = new TestUser { Id = "1", Email = Email, TwoFactorEnabled = true };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.GenerateTwoFactorTokenAsync(user, "Email")).ReturnsAsync("123456");
        var service = CreateService();

        var result = await service.SendTwoFaCodeAsync(
            new SendVerificationCodeRequestDto { Email = Email, Method = "Email" },
            ClaimsPrincipalHelper.ForUserId("someone-else"));

        Assert.True(result.Value!.IsSuccess);
        _emailSender.Verify(x => x.SendEmailAsync(Email, It.IsAny<string>(), It.Is<string>(b => b.Contains("123456"))), Times.Once);
    }

    [Fact]
    public async Task SendTwoFaCodeAsync_ForUnenrolledUser_RequiresSelfOrAdmin()
    {
        var user = new TestUser { Id = "1", Email = Email, TwoFactorEnabled = false };
        var otherUser = new TestUser { Id = "other-1" };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.FindByIdAsync("other-1")).ReturnsAsync(otherUser);
        _userManager.Setup(x => x.IsInRoleAsync(otherUser, "Admin")).ReturnsAsync(false);
        var service = CreateService();

        var result = await service.SendTwoFaCodeAsync(
            new SendVerificationCodeRequestDto { Email = Email, Method = "Email" },
            ClaimsPrincipalHelper.ForUserId("other-1"));

        Assert.Equal(Jwt2FaResultKind.BadRequest, result.Kind);
    }

    [Fact]
    public async Task SendTwoFaCodeAsync_ForAuthenticatorMethod_ReturnsFailureWithoutSending()
    {
        var user = new TestUser { Id = "1", Email = Email, TwoFactorEnabled = true };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.GenerateTwoFactorTokenAsync(user, "Authenticator")).ReturnsAsync("ignored");
        var service = CreateService();

        var result = await service.SendTwoFaCodeAsync(
            new SendVerificationCodeRequestDto { Email = Email, Method = "Authenticator" },
            ClaimsPrincipalHelper.ForUserId("1"));

        Assert.False(result.Value!.IsSuccess);
        _emailSender.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _smsSender.Verify(x => x.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendTwoFaCodeAsync_ForPhoneMethodWithNoPhoneNumber_ReturnsFailure()
    {
        var user = new TestUser { Id = "1", Email = Email, TwoFactorEnabled = true, PhoneNumber = null };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        _userManager.Setup(x => x.GenerateTwoFactorTokenAsync(user, "Phone")).ReturnsAsync("123456");
        var service = CreateService();

        var result = await service.SendTwoFaCodeAsync(
            new SendVerificationCodeRequestDto { Email = Email, Method = "Phone", PhoneNumber = "" },
            ClaimsPrincipalHelper.ForUserId("1"));

        Assert.False(result.Value!.IsSuccess);
    }

    [Fact]
    public async Task SendTwoFaCodeAsync_SwitchingToPhoneFromAnotherEnrolledMethod_UsesTheSubmittedPhoneNumber()
    {
        var user = new TestUser { Id = "1", Email = Email, TwoFactorEnabled = true, TwoFactorMethod = "Email", PhoneNumber = null };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        _userManager.Setup(x => x.GenerateTwoFactorTokenAsync(user, "Phone")).ReturnsAsync("123456");
        var service = CreateService();

        var result = await service.SendTwoFaCodeAsync(
            new SendVerificationCodeRequestDto { Email = Email, Method = "Phone", PhoneNumber = "5551234567" },
            ClaimsPrincipalHelper.ForUserId("1"));

        Assert.True(result.Value!.IsSuccess);
        _smsSender.Verify(x => x.SendSmsAsync("5551234567", It.Is<string>(b => b.Contains("123456")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendTwoFaCodeAsync_ForAlreadyVerifiedPhoneMethod_IgnoresASubmittedPhoneNumberAndUsesTheStoredOne()
    {
        var user = new TestUser { Id = "1", Email = Email, TwoFactorEnabled = true, TwoFactorMethod = "Phone", PhoneNumber = "5559999999" };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.GenerateTwoFactorTokenAsync(user, "Phone")).ReturnsAsync("123456");
        var service = CreateService();

        var result = await service.SendTwoFaCodeAsync(
            new SendVerificationCodeRequestDto { Email = Email, Method = "Phone", PhoneNumber = "5550000000" },
            ClaimsPrincipalHelper.ForUserId("someone-else"));

        Assert.True(result.Value!.IsSuccess);
        _smsSender.Verify(x => x.SendSmsAsync("5559999999", It.Is<string>(b => b.Contains("123456")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendTwoFaCodeAsync_SwitchingToPhoneFromAnotherEnrolledMethod_WhenNeitherSelfNorAdmin_ReturnsBadRequest()
    {
        var user = new TestUser { Id = "1", Email = Email, TwoFactorEnabled = true, TwoFactorMethod = "Email", PhoneNumber = null };
        var otherUser = new TestUser { Id = "other-1" };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.FindByIdAsync("other-1")).ReturnsAsync(otherUser);
        _userManager.Setup(x => x.IsInRoleAsync(otherUser, "Admin")).ReturnsAsync(false);
        var service = CreateService();

        var result = await service.SendTwoFaCodeAsync(
            new SendVerificationCodeRequestDto { Email = Email, Method = "Phone", PhoneNumber = "5551234567" },
            ClaimsPrincipalHelper.ForUserId("other-1"));

        Assert.Equal(Jwt2FaResultKind.BadRequest, result.Kind);
        _smsSender.Verify(x => x.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnableAuthenticatorAsync_WithNoExistingKey_GeneratesOneAndReturnsUri()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        _userManager.SetupSequence(x => x.GetAuthenticatorKeyAsync(user))
            .ReturnsAsync((string?)null)
            .ReturnsAsync("ABCDEFGHIJKLMNOP");
        _userManager.Setup(x => x.ResetAuthenticatorKeyAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.GetEmailAsync(user)).ReturnsAsync(Email);
        var service = CreateService();

        var result = await service.EnableAuthenticatorAsync(
            new EnableAuthenticatorRequestDto { Email = Email }, ClaimsPrincipalHelper.ForUserId("1"));

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        var response = Assert.IsType<EnableAuthenticatorResponseDto>(result.Value);
        Assert.Equal("abcd efgh ijkl mnop", response.SharedKey);
        _userManager.Verify(x => x.ResetAuthenticatorKeyAsync(user), Times.Once);
    }

    [Fact]
    public async Task EnableAuthenticatorAsync_WhenNeitherSelfNorAdmin_ReturnsBadRequest()
    {
        var user = new TestUser { Id = "1", Email = Email };
        var otherUser = new TestUser { Id = "other-1" };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.FindByIdAsync("other-1")).ReturnsAsync(otherUser);
        _userManager.Setup(x => x.IsInRoleAsync(otherUser, "Admin")).ReturnsAsync(false);
        var service = CreateService();

        var result = await service.EnableAuthenticatorAsync(
            new EnableAuthenticatorRequestDto { Email = Email }, ClaimsPrincipalHelper.ForUserId("other-1"));

        Assert.Equal(Jwt2FaResultKind.BadRequest, result.Kind);
    }

    [Fact]
    public async Task VerifyAuthenticatorAsync_WithInvalidCode_ReturnsNotVerified()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        _userManager.Setup(x => x.VerifyTwoFactorTokenAsync(user, "Authenticator", "000000")).ReturnsAsync(false);
        var service = CreateService();

        var result = await service.VerifyAuthenticatorAsync(
            new VerifyAuthenticatorRequestDto { Email = Email, Method = "Authenticator", Code = "000000" },
            ClaimsPrincipalHelper.ForUserId("1"));

        var response = Assert.IsType<VerifyAuthenticatorResponseDto>(result.Value);
        Assert.False(response.IsVerified);
    }

    [Fact]
    public async Task VerifyAuthenticatorAsync_WithValidAuthenticatorCode_EnablesAndReturnsRecoveryCodes()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        _userManager.Setup(x => x.VerifyTwoFactorTokenAsync(user, "Authenticator", "123456")).ReturnsAsync(true);
        _userManager.Setup(x => x.SetTwoFactorEnabledAsync(user, true)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManager
            .Setup(x => x.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))
            .ReturnsAsync(new[] { "code1", "code2" });
        var service = CreateService();

        var result = await service.VerifyAuthenticatorAsync(
            new VerifyAuthenticatorRequestDto { Email = Email, Method = "Authenticator", Code = "123456" },
            ClaimsPrincipalHelper.ForUserId("1"));

        var response = Assert.IsType<VerifyAuthenticatorResponseDto>(result.Value);
        Assert.True(response.IsVerified);
        Assert.Equal(new[] { "code1", "code2" }, response.Codes);
        Assert.Equal("Authenticator", user.TwoFactorMethod);
    }

    [Fact]
    public async Task VerifyAuthenticatorAsync_WithValidPhoneCode_SetsPhoneNumberAndNoRecoveryCodes()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        _userManager.Setup(x => x.VerifyTwoFactorTokenAsync(user, "Phone", "123456")).ReturnsAsync(true);
        _userManager.Setup(x => x.SetTwoFactorEnabledAsync(user, true)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        var service = CreateService();

        var result = await service.VerifyAuthenticatorAsync(
            new VerifyAuthenticatorRequestDto { Email = Email, Method = "Phone", Code = "123456", PhoneNumber = "+15551234" },
            ClaimsPrincipalHelper.ForUserId("1"));

        var response = Assert.IsType<VerifyAuthenticatorResponseDto>(result.Value);
        Assert.True(response.IsVerified);
        Assert.Null(response.Codes);
        Assert.Equal("+15551234", user.PhoneNumber);
        Assert.Equal("Phone", user.TwoFactorMethod);
    }

    [Fact]
    public async Task ResetAuthenticatorAsync_WithUnknownUser_ReturnsNotFound()
    {
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync((TestUser?)null);
        var service = CreateService();

        var result = await service.ResetAuthenticatorAsync(
            new EnableAuthenticatorRequestDto { Email = Email }, ClaimsPrincipalHelper.ForUserId("1"));

        Assert.Equal(Jwt2FaResultKind.NotFound, result.Kind);
    }

    [Fact]
    public async Task ResetAuthenticatorAsync_OnSuccess_ClearsMethodAndDisables2Fa()
    {
        var user = new TestUser { Id = "1", Email = Email, TwoFactorMethod = "Authenticator" };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        _userManager.Setup(x => x.SetTwoFactorEnabledAsync(user, false)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.ResetAuthenticatorKeyAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _signInManager.Setup(x => x.RefreshSignInAsync(user)).Returns(Task.CompletedTask);
        var service = CreateService();

        var result = await service.ResetAuthenticatorAsync(
            new EnableAuthenticatorRequestDto { Email = Email }, ClaimsPrincipalHelper.ForUserId("1"));

        Assert.True(result.Value!.IsSuccess);
        Assert.Null(user.TwoFactorMethod);
        _userManager.Verify(x => x.SetTwoFactorEnabledAsync(user, false), Times.Once);
    }
}
