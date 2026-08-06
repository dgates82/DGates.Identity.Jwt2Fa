using DGates.Identity.Jwt2Fa.Capabilities;
using Microsoft.AspNetCore.Identity;

namespace DGates.Identity.Jwt2Fa.Tests.Fixtures;

/// <summary>Minimal <see cref="IdentityUser"/> implementing all three capability interfaces, for service tests.</summary>
public class TestUser : IdentityUser, IActivatableUser, IAdminProvisionableUser, IMultiFactorMethodUser
{
    public bool IsActive { get; set; } = true;

    public bool HasSetPassword { get; set; }

    public string? TwoFactorMethod { get; set; }
}
