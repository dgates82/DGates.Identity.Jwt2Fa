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
        _jwtOptions,
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
    public async Task RegisterAsync_WithSucceedingCreate_SetsHasSetPassword()
    {
        TestUser? createdUser = null;
        _userManager.Setup(x => x.CreateAsync(It.IsAny<TestUser>(), Password))
            .Callback<TestUser, string>((user, _) => createdUser = user)
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<TestUser>())).ReturnsAsync("raw-code");
        var service = CreateService();

        await service.RegisterAsync(new RegisterRequestDto { Email = Email, Password = Password });

        Assert.True(createdUser!.HasSetPassword);
        _userManager.Verify(x => x.UpdateAsync(createdUser), Times.Once);
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
    public async Task LoginAsync_WithSuccessfulSignIn_PopulatesRolesBeforeProjecting()
    {
        var user = new TestUser { Id = "1", Email = Email, TwoFactorEnabled = false };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        _userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _signInManager.Setup(x => x.PasswordSignInAsync(Email, Password, false, false)).ReturnsAsync(SignInResult.Success);
        SetUpJwtTokenServiceToReturn("signed.jwt.token");
        var service = CreateService();

        await service.LoginAsync(new AuthRequestDto { Email = Email, Password = Password });

        Assert.Equal(new[] { "Admin" }, user.Roles);
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
            Code = Base64UrlEncode("code")
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
            Code = Base64UrlEncode("code")
        });

        Assert.False(result.Value!.IsSuccess);
        Assert.False(user.HasSetPassword);
        _userManager.Verify(x => x.UpdateAsync(It.IsAny<TestUser>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithUserNotImplementingCapability_StillSucceeds()
    {
        var bareUserManager = IdentityMockFactory.CreateUserManagerMock<BareUser>();
        var bareSignInManager = IdentityMockFactory.CreateSignInManagerMock(bareUserManager.Object);
        var user = new BareUser { Id = "1", Email = Email };
        bareUserManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        bareUserManager
            .Setup(x => x.ResetPasswordAsync(user, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        var service = new AuthCoreService<BareUser>(
            bareUserManager.Object,
            bareSignInManager.Object,
            Mock.Of<IJwtTokenService<BareUser>>(),
            u => new { u.Id },
            _emailSender.Object,
            _authCoreOptions,
            _jwtOptions);

        var result = await service.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Email = Email,
            Password = "NewP@ss1",
            Code = Base64UrlEncode("code")
        });

        Assert.True(result.Value!.IsSuccess);
        bareUserManager.Verify(x => x.UpdateAsync(It.IsAny<BareUser>()), Times.Never);
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
    public async Task SendEmailConfirmationAsync_WithUserNotImplementingCapability_OmitsFirstLoginParams()
    {
        var bareUserManager = IdentityMockFactory.CreateUserManagerMock<BareUser>();
        var bareSignInManager = IdentityMockFactory.CreateSignInManagerMock(bareUserManager.Object);
        var user = new BareUser { Id = "1", Email = Email };
        bareUserManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        bareUserManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(user)).ReturnsAsync("email-code");
        string? capturedBody = null;
        _emailSender
            .Setup(x => x.SendEmailAsync(Email, It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, body) => capturedBody = body)
            .Returns(Task.CompletedTask);
        var service = new AuthCoreService<BareUser>(
            bareUserManager.Object,
            bareSignInManager.Object,
            Mock.Of<IJwtTokenService<BareUser>>(),
            u => new { u.Id },
            _emailSender.Object,
            _authCoreOptions,
            _jwtOptions);

        await service.SendEmailConfirmationAsync(new SendEmailConfirmationRequestDto { Email = Email });

        Assert.DoesNotContain("isFirstLogin", capturedBody);
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
            Code = Base64UrlEncode("code")
        });

        Assert.True(result.Value!.IsSuccess);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WithUnknownEmail_ReturnsOkWithFailureDto()
    {
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync((TestUser?)null);
        var service = CreateService();

        var result = await service.GetUserByEmailAsync(Email, ClaimsPrincipalHelper.ForUserId("caller-1"));

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        var dto = Assert.IsType<ResponseDto>(result.Value);
        Assert.False(dto.IsSuccess);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenCallerIsSelf_ReturnsProjectedUser()
    {
        var target = new TestUser { Id = "user-1", Email = Email };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(target);
        _userManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(target);
        _userManager.Setup(x => x.IsInRoleAsync(target, "Admin")).ReturnsAsync(false);
        _userManager.Setup(x => x.GetRolesAsync(target)).ReturnsAsync(new List<string>());
        var service = CreateService();

        var result = await service.GetUserByEmailAsync(Email, ClaimsPrincipalHelper.ForUserId("user-1"));

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenCallerIsSelf_PopulatesRolesBeforeProjecting()
    {
        var target = new TestUser { Id = "user-1", Email = Email };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(target);
        _userManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(target);
        _userManager.Setup(x => x.IsInRoleAsync(target, "Admin")).ReturnsAsync(false);
        _userManager.Setup(x => x.GetRolesAsync(target)).ReturnsAsync(new List<string> { "Admin" });
        var service = CreateService();

        await service.GetUserByEmailAsync(Email, ClaimsPrincipalHelper.ForUserId("user-1"));

        Assert.Equal(new[] { "Admin" }, target.Roles);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenCallerIsAdmin_ReturnsProjectedUser()
    {
        var target = new TestUser { Id = "user-1", Email = Email };
        var admin = new TestUser { Id = "admin-1", Email = "admin@example.com" };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(target);
        _userManager.Setup(x => x.FindByIdAsync("admin-1")).ReturnsAsync(admin);
        _userManager.Setup(x => x.IsInRoleAsync(admin, "Admin")).ReturnsAsync(true);
        _userManager.Setup(x => x.GetRolesAsync(target)).ReturnsAsync(new List<string>());
        var service = CreateService();

        var result = await service.GetUserByEmailAsync(Email, ClaimsPrincipalHelper.ForUserId("admin-1"));

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenCallerIsNeitherSelfNorAdmin_ReturnsBadRequest()
    {
        var target = new TestUser { Id = "user-1", Email = Email };
        var otherUser = new TestUser { Id = "other-1", Email = "other@example.com" };
        _userManager.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(target);
        _userManager.Setup(x => x.FindByIdAsync("other-1")).ReturnsAsync(otherUser);
        _userManager.Setup(x => x.IsInRoleAsync(otherUser, "Admin")).ReturnsAsync(false);
        var service = CreateService();

        var result = await service.GetUserByEmailAsync(Email, ClaimsPrincipalHelper.ForUserId("other-1"));

        Assert.Equal(Jwt2FaResultKind.BadRequest, result.Kind);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithUnknownId_ReturnsNotFound()
    {
        _userManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((TestUser?)null);
        var service = CreateService();

        var result = await service.GetUserByIdAsync("missing");

        Assert.Equal(Jwt2FaResultKind.NotFound, result.Kind);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithKnownId_PopulatesRolesAndReturnsProjectedUser()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        var service = CreateService();

        var result = await service.GetUserByIdAsync("1");

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.NotNull(result.Value);
        Assert.Equal(new[] { "Admin" }, user.Roles);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithUserNotImplementingCapability_SkipsRolePopulation()
    {
        var bareUserManager = IdentityMockFactory.CreateUserManagerMock<BareUser>();
        var bareSignInManager = IdentityMockFactory.CreateSignInManagerMock(bareUserManager.Object);
        var user = new BareUser { Id = "1", Email = Email };
        bareUserManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        var service = new AuthCoreService<BareUser>(
            bareUserManager.Object,
            bareSignInManager.Object,
            Mock.Of<IJwtTokenService<BareUser>>(),
            u => new { u.Id },
            _emailSender.Object,
            _authCoreOptions,
            _jwtOptions);

        var result = await service.GetUserByIdAsync("1");

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.NotNull(result.Value);
        bareUserManager.Verify(x => x.GetRolesAsync(It.IsAny<BareUser>()), Times.Never);
    }

    [Fact]
    public async Task ListUsersAsync_ReturnsPagedResultsWithRolesPopulated()
    {
        var userA = new TestUser { Id = "1", Email = "a@example.com" };
        var userB = new TestUser { Id = "2", Email = "b@example.com" };
        _userManager.Setup(x => x.Users).Returns(new[] { userA, userB }.AsQueryable());
        _userManager.Setup(x => x.GetRolesAsync(userA)).ReturnsAsync(new List<string> { "Admin" });
        _userManager.Setup(x => x.GetRolesAsync(userB)).ReturnsAsync(new List<string>());
        var service = CreateService();

        var result = await service.ListUsersAsync(page: 1, pageSize: 20);

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(new[] { "Admin" }, userA.Roles);
        Assert.Empty(userB.Roles);
    }

    [Fact]
    public async Task ListUsersAsync_WithUserNotImplementingCapability_SkipsRolePopulation()
    {
        var bareUserManager = IdentityMockFactory.CreateUserManagerMock<BareUser>();
        var bareSignInManager = IdentityMockFactory.CreateSignInManagerMock(bareUserManager.Object);
        var userA = new BareUser { Id = "1", Email = "a@example.com" };
        var userB = new BareUser { Id = "2", Email = "b@example.com" };
        bareUserManager.Setup(x => x.Users).Returns(new[] { userA, userB }.AsQueryable());
        var service = new AuthCoreService<BareUser>(
            bareUserManager.Object,
            bareSignInManager.Object,
            Mock.Of<IJwtTokenService<BareUser>>(),
            u => new { u.Id },
            _emailSender.Object,
            _authCoreOptions,
            _jwtOptions);

        var result = await service.ListUsersAsync(page: 1, pageSize: 20);

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.Equal(2, result.Value.Items.Count);
        bareUserManager.Verify(x => x.GetRolesAsync(It.IsAny<BareUser>()), Times.Never);
    }

    [Fact]
    public async Task ListUsersAsync_RespectsPageSize()
    {
        var users = Enumerable.Range(1, 5)
            .Select(i => new TestUser { Id = i.ToString(), Email = $"user{i}@example.com" })
            .ToArray();
        _userManager.Setup(x => x.Users).Returns(users.AsQueryable());
        _userManager.Setup(x => x.GetRolesAsync(It.IsAny<TestUser>())).ReturnsAsync(new List<string>());
        var service = CreateService();

        var result = await service.ListUsersAsync(page: 2, pageSize: 2);

        Assert.Equal(5, result.Value!.TotalCount);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(2, result.Value.PageSize);
    }

    [Fact]
    public async Task AdminCreateUserAsync_OnSuccess_SetsHasSetPasswordFalseAssignsRolesAndSendsEmail()
    {
        _userManager.Setup(x => x.CreateAsync(It.IsAny<TestUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.AddToRoleAsync(It.IsAny<TestUser>(), "Support")).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<TestUser>())).ReturnsAsync("email-code");
        _userManager.Setup(x => x.GeneratePasswordResetTokenAsync(It.IsAny<TestUser>())).ReturnsAsync("reset-code");
        _userManager.Setup(x => x.GetRolesAsync(It.IsAny<TestUser>())).ReturnsAsync(new List<string> { "Support" });
        var service = CreateService();

        var result = await service.AdminCreateUserAsync(new AdminCreateUserRequestDto
        {
            Email = Email,
            Roles = new[] { "Support" }
        });

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.NotNull(result.Value);
        _userManager.Verify(x => x.AddToRoleAsync(It.IsAny<TestUser>(), "Support"), Times.Once);
        _emailSender.Verify(x => x.SendEmailAsync(Email, It.IsAny<string>(),
            It.Is<string>(body => body.Contains("isFirstLogin=true"))), Times.Once);
    }

    [Fact]
    public async Task AdminCreateUserAsync_WithFailingCreate_ReturnsBadRequestAndSendsNoEmail()
    {
        var errors = new[] { new IdentityError { Code = "DuplicateEmail", Description = "Email taken." } };
        _userManager.Setup(x => x.CreateAsync(It.IsAny<TestUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Failed(errors));
        var service = CreateService();

        var result = await service.AdminCreateUserAsync(new AdminCreateUserRequestDto { Email = Email });

        Assert.Equal(Jwt2FaResultKind.BadRequest, result.Kind);
        _emailSender.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ListUsersAsync_ClampsPageSizeToMaxPageSize()
    {
        var users = Enumerable.Range(1, 5)
            .Select(i => new TestUser { Id = i.ToString(), Email = $"user{i}@example.com" })
            .ToArray();
        _userManager.Setup(x => x.Users).Returns(users.AsQueryable());
        _userManager.Setup(x => x.GetRolesAsync(It.IsAny<TestUser>())).ReturnsAsync(new List<string>());
        var options = Options.Create(new AuthCoreOptions
        {
            ApplicationName = "Test App",
            FrontendBaseUrl = "https://app.example.com",
            EmailConfirmationPath = "/email-confirmation?userId={userId}&code={code}",
            ForgotPasswordPath = "/forgot-password/reset?code={code}",
            MaxPageSize = 3
        });
        var service = new AuthCoreService<TestUser>(
            _userManager.Object,
            _signInManager.Object,
            _jwtTokenService.Object,
            user => new { user.Id },
            _emailSender.Object,
            options,
            _jwtOptions);

        var result = await service.ListUsersAsync(page: 1, pageSize: 1000);

        Assert.Equal(3, result.Value!.PageSize);
        Assert.Equal(3, result.Value.Items.Count);
    }

    [Fact]
    public async Task AdminUpdateUserAsync_WithUnknownId_ReturnsNotFound()
    {
        _userManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((TestUser?)null);
        var service = CreateService();

        var result = await service.AdminUpdateUserAsync("missing", new AdminUpdateUserRequestDto { Email = Email });

        Assert.Equal(Jwt2FaResultKind.NotFound, result.Kind);
    }

    [Fact]
    public async Task AdminUpdateUserAsync_WithChangedEmail_UpdatesEmailAndUserNameAndSendsConfirmation()
    {
        var user = new TestUser { Id = "1", Email = Email, UserName = Email };
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.SetEmailAsync(user, "new@example.com")).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.SetUserNameAsync(user, "new@example.com")).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(user)).ReturnsAsync("email-code");
        _userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        var service = CreateService();

        var result = await service.AdminUpdateUserAsync("1", new AdminUpdateUserRequestDto { Email = "new@example.com" });

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        _userManager.Verify(x => x.SetEmailAsync(user, "new@example.com"), Times.Once);
        _userManager.Verify(x => x.SetUserNameAsync(user, "new@example.com"), Times.Once);
        _emailSender.Verify(x => x.SendEmailAsync("new@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task AdminUpdateUserAsync_WithUnchangedEmail_DoesNotCallSetEmail()
    {
        var user = new TestUser { Id = "1", Email = Email, UserName = Email };
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        var service = CreateService();

        var result = await service.AdminUpdateUserAsync("1", new AdminUpdateUserRequestDto { Email = Email });

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        _userManager.Verify(x => x.SetEmailAsync(It.IsAny<TestUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AdminUpdateUserAsync_DiffsRoles_AddingAndRemovingOnlyWhatChanged()
    {
        var user = new TestUser { Id = "1", Email = Email, UserName = Email };
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Support", "Legacy" });
        _userManager
            .Setup(x => x.AddToRolesAsync(user, It.Is<IEnumerable<string>>(r => r.Single() == "Admin")))
            .ReturnsAsync(IdentityResult.Success);
        _userManager
            .Setup(x => x.RemoveFromRolesAsync(user, It.Is<IEnumerable<string>>(r => r.Single() == "Legacy")))
            .ReturnsAsync(IdentityResult.Success);
        var service = CreateService();

        var result = await service.AdminUpdateUserAsync("1", new AdminUpdateUserRequestDto
        {
            Email = Email,
            Roles = new[] { "Support", "Admin" }
        });

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        _userManager.Verify(x => x.AddToRolesAsync(user, It.Is<IEnumerable<string>>(r => r.Single() == "Admin")), Times.Once);
        _userManager.Verify(x => x.RemoveFromRolesAsync(user, It.Is<IEnumerable<string>>(r => r.Single() == "Legacy")), Times.Once);
    }

    [Fact]
    public async Task AdminUpdateUserAsync_WithFailingRoleAssignment_ReturnsBadRequest()
    {
        var user = new TestUser { Id = "1", Email = Email, UserName = Email };
        var errors = new[] { new IdentityError { Code = "RoleNotFound", Description = "Role does not exist." } };
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        _userManager
            .Setup(x => x.AddToRolesAsync(user, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Failed(errors));
        var service = CreateService();

        var result = await service.AdminUpdateUserAsync("1", new AdminUpdateUserRequestDto
        {
            Email = Email,
            Roles = new[] { "Nonexistent" }
        });

        Assert.Equal(Jwt2FaResultKind.BadRequest, result.Kind);
    }

    [Fact]
    public async Task AdminUnlockUserAsync_WithUnknownId_ReturnsNotFound()
    {
        _userManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((TestUser?)null);
        var service = CreateService();

        var result = await service.AdminUnlockUserAsync("missing");

        Assert.Equal(Jwt2FaResultKind.NotFound, result.Kind);
    }

    [Fact]
    public async Task AdminUnlockUserAsync_SetsLockoutEndToNow()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager
            .Setup(x => x.SetLockoutEndDateAsync(user, It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync(IdentityResult.Success);
        var service = CreateService();

        var result = await service.AdminUnlockUserAsync("1");

        Assert.True(result.Value!.IsSuccess);
        _userManager.Verify(
            x => x.SetLockoutEndDateAsync(user, It.Is<DateTimeOffset?>(d => d.HasValue && d.Value <= DateTimeOffset.UtcNow)),
            Times.Once);
    }

    [Fact]
    public async Task AdminUnlockUserAsync_WhenStoreUpdateFails_ReturnsUnsuccessfulResponseWithMessage()
    {
        var user = new TestUser { Id = "1", Email = Email };
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager
            .Setup(x => x.SetLockoutEndDateAsync(user, It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Store does not support lockout." }));
        var service = CreateService();

        var result = await service.AdminUnlockUserAsync("1");

        Assert.False(result.Value!.IsSuccess);
        Assert.Contains("Store does not support lockout.", result.Value.Message);
    }

    private static string Base64UrlEncode(string value) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

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
