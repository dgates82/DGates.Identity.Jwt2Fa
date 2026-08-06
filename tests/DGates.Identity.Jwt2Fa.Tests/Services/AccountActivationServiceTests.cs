using DGates.Identity.Jwt2Fa.Jwt;
using DGates.Identity.Jwt2Fa.Services;
using DGates.Identity.Jwt2Fa.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;

namespace DGates.Identity.Jwt2Fa.Tests.Services;

public class AccountActivationServiceTests
{
    private const string TargetEmail = "target@example.com";

    private readonly Mock<UserManager<TestUser>> _userManager = IdentityMockFactory.CreateUserManagerMock<TestUser>();
    private readonly IOptions<JwtOptions> _jwtOptions = Options.Create(new JwtOptions
    {
        SecurityKey = "test-key-test-key-test-key-1234",
        ValidIssuer = "issuer",
        ValidAudience = "audience",
        AdminRoleName = "Admin"
    });

    private AccountActivationService<TestUser> CreateService() =>
        new(_userManager.Object, _jwtOptions, user => new { user.Id });

    [Fact]
    public async Task GetUserByEmailAsync_WithUnknownEmail_ReturnsOkWithFailureDto()
    {
        _userManager.Setup(x => x.FindByEmailAsync(TargetEmail)).ReturnsAsync((TestUser?)null);
        var service = CreateService();

        var result = await service.GetUserByEmailAsync(TargetEmail, ClaimsPrincipalHelper.ForUserId("caller-1"));

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        var dto = Assert.IsType<Dtos.ResponseDto>(result.Value);
        Assert.False(dto.IsSuccess);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenCallerIsSelf_ReturnsProjectedUser()
    {
        var target = new TestUser { Id = "user-1", Email = TargetEmail };
        _userManager.Setup(x => x.FindByEmailAsync(TargetEmail)).ReturnsAsync(target);
        _userManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(target);
        _userManager.Setup(x => x.IsInRoleAsync(target, "Admin")).ReturnsAsync(false);
        var service = CreateService();

        var result = await service.GetUserByEmailAsync(TargetEmail, ClaimsPrincipalHelper.ForUserId("user-1"));

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenCallerIsAdmin_ReturnsProjectedUser()
    {
        var target = new TestUser { Id = "user-1", Email = TargetEmail };
        var admin = new TestUser { Id = "admin-1", Email = "admin@example.com" };
        _userManager.Setup(x => x.FindByEmailAsync(TargetEmail)).ReturnsAsync(target);
        _userManager.Setup(x => x.FindByIdAsync("admin-1")).ReturnsAsync(admin);
        _userManager.Setup(x => x.IsInRoleAsync(admin, "Admin")).ReturnsAsync(true);
        var service = CreateService();

        var result = await service.GetUserByEmailAsync(TargetEmail, ClaimsPrincipalHelper.ForUserId("admin-1"));

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenCallerIsNeitherSelfNorAdmin_ReturnsBadRequest()
    {
        var target = new TestUser { Id = "user-1", Email = TargetEmail };
        var otherUser = new TestUser { Id = "other-1", Email = "other@example.com" };
        _userManager.Setup(x => x.FindByEmailAsync(TargetEmail)).ReturnsAsync(target);
        _userManager.Setup(x => x.FindByIdAsync("other-1")).ReturnsAsync(otherUser);
        _userManager.Setup(x => x.IsInRoleAsync(otherUser, "Admin")).ReturnsAsync(false);
        var service = CreateService();

        var result = await service.GetUserByEmailAsync(TargetEmail, ClaimsPrincipalHelper.ForUserId("other-1"));

        Assert.Equal(Jwt2FaResultKind.BadRequest, result.Kind);
    }
}
