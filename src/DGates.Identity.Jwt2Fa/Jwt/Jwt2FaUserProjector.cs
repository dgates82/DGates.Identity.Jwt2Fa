namespace DGates.Identity.Jwt2Fa.Jwt;

/// <summary>
/// Projects a <c>TUser</c> down to whatever trimmed, client-safe shape the consumer
/// wants embedded in the JWT's <c>"user"</c> claim and returned in auth responses —
/// e.g. omitting <c>PasswordHash</c>/<c>SecurityStamp</c>. Registered once via
/// <c>AddAuthCore</c>; there is no default projection, since what's safe to expose
/// is entirely consumer-specific.
/// </summary>
public delegate object Jwt2FaUserProjector<in TUser>(TUser user);
