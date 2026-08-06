using DGates.Identity.Jwt2Fa.Capabilities;
using DGates.Identity.Jwt2Fa.Dtos;
using DGates.Identity.Jwt2Fa.Extensions;
using DGates.Identity.Jwt2Fa.Jwt;
using DGates.Identity.Jwt2Fa.Services;
using DGates.Identity.Jwt2Fa.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace DGates.Identity.Jwt2Fa.Tests.Services;

public class AuthCoreServiceTests
{
    private const string Email = "user@example.com";
    private const string Password = "P@ssw0rd!";

    private readonly Mock<UserManager<TestUser>> _userManager = IdentityMockFactory.CreateUserManagerMock<TestUser>();
    private readonly Mock<SignInManager<TestUser>> _signInManager;
    private readonly Mock<IJwtTokenService<TestUser>> _jwtTokenService = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly IOptions<AuthCoreOptions> _authCoreOptions = Options.Create(new AuthCoreOptions
    {
        ApplicationName = "Test App",
        EmailConfirmationCallbackUrl = "https://app.example.com/email-confirmation?userId={userId}&code={code}"
    });

    public AuthCoreServiceTests()
    {
        _signInManager = IdentityMockFactory.CreateSignInManagerMock(_userManager.Object);
    }

    private AuthCoreService<TestUser> CreateService(IActivationPolicy<TestUser>? activationPolicy = null) => new(
        _userManager.Object,
        _signInManager.Object,
        _jwtTokenService.Object,
        user => new { user.Id },
        _emailSender.Object,
        _authCoreOptions,
        activationPolicy);

    [Fact]
    public async Task RegisterAsync_WithSucceedingCreate_SendsConfirmationEmailAndReturnsOk()
    {
        _userManager.Setup(x => x.CreateAsync(It.IsAny<TestUser>(), Password)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<TestUser>())).ReturnsAsync("raw-code");
        var service = CreateService();

        var result = await service.RegisterAsync(new RegisterRequestDto { Email = Email, Password = Password });

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.True(result.Value!.IsSuccess);
        _emailSender.Verify(x => x.SendEmailAsync(Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WithFailingCreate_ReturnsBadRequestAndSendsNoEmail()
    {
        var errors = new[] { new IdentityError { Code = "DuplicateEmail", Description = "Email taken." } };
        _userManager.Setup(x => x.CreateAsync(It.IsAny<TestUser>(), Password)).ReturnsAsync(IdentityResult.Failed(errors));
        var service = CreateService();

        var result = await service.RegisterAsync(new RegisterRequestDto { Email = Email, Password = Password });

        Assert.Equal(Jwt2FaResultKind.BadRequest, result.Kind);
        var actualErrors = Assert.IsAssignableFrom<IEnumerable<IdentityError>>(result.Error);
        Assert.Equal(errors.Select(e => e.Code), actualErrors.Select(e => e.Code));
        _emailSender.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownUser_ReturnsOkWithoutRevealingNonExistence()
    {
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync((TestUser?)null);
        var service = CreateService();

        var result = await service.LoginAsync(new AuthRequestDto { Email = Email, Password = Password });

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.False(result.Value!.IsAuthSuccessful);
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUserAndActivationPolicy_ReturnsOkWithoutAttemptingSignIn()
    {
        var user = new TestUser { Id = "1", Email = Email, IsActive = false };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        var policy = new PropertyBackedActivationPolicy<TestUser>();
        var service = CreateService(policy);

        var result = await service.LoginAsync(new AuthRequestDto { Email = Email, Password = Password });

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.False(result.Value!.IsAuthSuccessful);
        Assert.Equal("User is not active", result.Value.ErrorMessage);
        _signInManager.Verify(
            x => x.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUserAndNoActivationPolicy_StillAttemptsSignIn()
    {
        var user = new TestUser { Id = "1", Email = Email, IsActive = false };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.AccessFailedAsync(user)).ReturnsAsync(IdentityResult.Success);
        _signInManager
            .Setup(x => x.PasswordSignInAsync(Email, Password, false, false))
            .ReturnsAsync(SignInResult.Failed);
        var service = CreateService(activationPolicy: null);

        await service.LoginAsync(new AuthRequestDto { Email = Email, Password = Password });

        _signInManager.Verify(x => x.PasswordSignInAsync(Email, Password, false, false), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WithSuccessfulSignIn_ReturnsTokenAndProjectedUser()
    {
        var user = new TestUser { Id = "1", Email = Email, TwoFactorEnabled = false };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        _signInManager.Setup(x => x.PasswordSignInAsync(Email, Password, false, false)).ReturnsAsync(SignInResult.Success);
        SetUpJwtTokenServiceToReturn("signed.jwt.token");
        var service = CreateService();

        var result = await service.LoginAsync(new AuthRequestDto { Email = Email, Password = Password });

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.True(result.Value!.IsAuthSuccessful);
        Assert.Equal("signed.jwt.token", result.Value.Token);
        Assert.False(result.Value.RequiresTwoFactor);
        Assert.NotNull(result.Value.User);
    }

    [Fact]
    public async Task LoginAsync_WhenTwoFactorRequired_ReturnsPhoneNumberOnlyForPhoneMethod()
    {
        var user = new TestUser { Id = "1", Email = Email, TwoFactorMethod = "Phone", PhoneNumber = "+15550000" };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _signInManager
            .Setup(x => x.PasswordSignInAsync(Email, Password, false, false))
            .ReturnsAsync(SignInResult.TwoFactorRequired);
        var service = CreateService();

        var result = await service.LoginAsync(new AuthRequestDto { Email = Email, Password = Password });

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.True(result.Value!.RequiresTwoFactor);
        Assert.Equal("Phone", result.Value.TwoFactorMethod);
        Assert.Equal("+15550000", result.Value.PhoneNumber);
    }

    [Fact]
    public async Task LoginAsync_WhenTwoFactorRequiredViaEmail_LeavesPhoneNumberEmpty()
    {
        var user = new TestUser { Id = "1", Email = Email, TwoFactorMethod = "Email", PhoneNumber = "+15550000" };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _signInManager
            .Setup(x => x.PasswordSignInAsync(Email, Password, false, false))
            .ReturnsAsync(SignInResult.TwoFactorRequired);
        var service = CreateService();

        var result = await service.LoginAsync(new AuthRequestDto { Email = Email, Password = Password });

        Assert.Equal("", result.Value!.PhoneNumber);
    }

    [Fact]
    public async Task LoginAsync_WithBadCredentials_ReturnsUnauthorizedAndRecordsFailedAccess()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.AccessFailedAsync(user)).ReturnsAsync(IdentityResult.Success);
        _signInManager.Setup(x => x.PasswordSignInAsync(Email, Password, false, false)).ReturnsAsync(SignInResult.Failed);
        var service = CreateService();

        var result = await service.LoginAsync(new AuthRequestDto { Email = Email, Password = Password });

        Assert.Equal(Jwt2FaResultKind.Unauthorized, result.Kind);
        Assert.Equal("Invalid Authentication", result.Value!.ErrorMessage);
        _userManager.Verify(x => x.AccessFailedAsync(user), Times.Once);
    }

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
}
