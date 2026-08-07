namespace DGates.Identity.Jwt2Fa.Capabilities;

/// <summary>
/// Marks a user type that can carry a populated roles list. <see cref="Jwt2Fa.Jwt.Jwt2FaUserProjector{TUser}"/>
/// is synchronous and can't itself call <c>UserManager.GetRolesAsync</c>, so endpoints
/// that want roles in their projected response (the admin lookup/list endpoints) populate
/// this before projecting, if <c>TUser</c> implements it — same opportunistic-enhancement
/// pattern as <see cref="IAdminProvisionableUser"/>/<see cref="IMultiFactorMethodUser"/>.
/// </summary>
public interface IRoleAwareUser
{
    /// <summary>The Identity roles assigned to this user. Not meant to be persisted directly — Identity's role tables are the source of truth.</summary>
    List<string> Roles { get; set; }
}
