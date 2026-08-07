using DGates.Identity.Jwt2Fa.Services;
using DGates.Identity.Jwt2Fa.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace DGates.Identity.Jwt2Fa.Tests.Services;

public class UserActivationServiceTests
{
    private readonly Mock<UserManager<TestUser>> _userManager = IdentityMockFactory.CreateUserManagerMock<TestUser>();

    private UserActivationService<TestUser> CreateService() => new(_userManager.Object);

    [Fact]
    public async Task ActivateAsync_WithUnknownId_ReturnsNotFound()
    {
        _userManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((TestUser?)null);
        var service = CreateService();

        var result = await service.ActivateAsync("missing");

        Assert.Equal(Jwt2FaResultKind.NotFound, result.Kind);
    }

    [Fact]
    public async Task ActivateAsync_SetsIsActiveTrueAndPersists()
    {
        var user = new TestUser { Id = "1", IsActive = false };
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        var service = CreateService();

        var result = await service.ActivateAsync("1");

        Assert.True(result.Value!.IsSuccess);
        Assert.True(user.IsActive);
        _userManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveFalseAndPersists()
    {
        var user = new TestUser { Id = "1", IsActive = true };
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        var service = CreateService();

        var result = await service.DeactivateAsync("1");

        Assert.True(result.Value!.IsSuccess);
        Assert.False(user.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_WhenUpdateFails_ReturnsUnsuccessfulResponseWithMessage()
    {
        var user = new TestUser { Id = "1", IsActive = true };
        _userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManager
            .Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Concurrency conflict." }));
        var service = CreateService();

        var result = await service.DeactivateAsync("1");

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.False(result.Value!.IsSuccess);
        Assert.Contains("Concurrency conflict.", result.Value.Message);
    }
}
