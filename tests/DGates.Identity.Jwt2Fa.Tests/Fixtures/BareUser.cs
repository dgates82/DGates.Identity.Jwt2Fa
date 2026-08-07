using Microsoft.AspNetCore.Identity;

namespace DGates.Identity.Jwt2Fa.Tests.Fixtures;

/// <summary>A plain <see cref="IdentityUser"/> implementing none of the capability interfaces, for proving optional-cast behavior degrades gracefully.</summary>
public class BareUser : IdentityUser;
