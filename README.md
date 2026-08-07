# DGates.Identity.Jwt2Fa

Real, claims-bearing JWTs and multi-channel two-factor authentication
(Authenticator/TOTP, Email, SMS) for ASP.NET Core Identity — generic over your own
user type.

Targets **.NET 10 only** — not compatible with .NET Framework (e.g. net48).

> **Status:** pre-release. The core port and its test suite are done and passing,
> but nothing has been published to NuGet.org yet and the API surface may still move
> before `v1.0.0`.

## Why this instead of `MapIdentityApi<TUser>`?

ASP.NET Core's built-in `AddIdentityApiEndpoints<TUser>()`/`MapIdentityApi<TUser>()`
(since .NET 8) already covers "Identity as a JSON API" — that's not the pitch here.
Two things it doesn't cover:

- **Multi-channel 2FA.** Its two-factor support is TOTP-authenticator +
  recovery-codes only — no SMS or email OTP delivery. This package adds both, using
  the same `IUserTwoFactorTokenProvider<TUser>` mechanism Identity already has, not a
  bolted-on parallel system.
- **Real JWTs.** Its bearer tokens are opaque, Data-Protection-encrypted tokens — no
  readable claims, no interop with other JWT-consuming systems. This package issues
  standard signed JWTs with your own projected user claim embedded.

[Duende IdentityServer](https://duendesoftware.com/products/identityserver) is the
other "real JWTs + Identity" option, but it solves a different, larger problem (a
full OAuth2/OIDC provider for third-party clients) and requires a paid license above
a small revenue threshold. If you need that, use it — this package is for a simpler
case: your own first-party client(s) authenticating directly against your own API.

## Install

```sh
dotnet add package DGates.Identity.Jwt2Fa
```

You'll also need an `IEmailSender` (`Microsoft.AspNetCore.Identity.UI.Services.IEmailSender`
— used by registration, password reset, and email 2FA) and, if you use SMS 2FA, an
`ISmsSender` — this package doesn't ship notification delivery itself.
[`DGates.Identity.NotificationProviders`](https://github.com/dgates82/DGates.Identity.NotificationProviders)
covers both in one package (SMTP/SendGrid/Postmark for email, Twilio/AWS SNS for SMS),
or bring your own implementation of either interface.

## Design: opt-in modules, capability interfaces instead of one big user contract

Everything is generic over `TUser : IdentityUser`, but the two pieces below aren't one
monolithic registration — each is opt-in, and each capability beyond bare `IdentityUser`
is its own small interface your `TUser` implements only if you use the module that
needs it. Skip a module, never see its interface. Opt into one without the matching
interface, and it's a compile error, not a silent no-op.

| Module | Registration | Requires on `TUser` | Endpoints |
|---|---|---|---|
| Core | `AddAuthCore<TUser>()` + `MapAuthCore<TUser>()` | nothing extra (optionally honors `IActivationPolicy<TUser>` if registered — see below) | register, login, secure, getuserbyemail, getuserbyid, listusers, admincreateuser, forgotpassword, resetpassword, changepassword, sendemailconfirmation, confirmEmail |
| Two-factor | `Add2Fa<TUser>()` + `Map2Fa<TUser>()` | `IMultiFactorMethodUser` | login2fa, sendtwofacode, enableauthenticator, verifyauthenticator, resetauthenticator |

`getuserbyid`, `listusers`, and `admincreateuser` require the `Jwt2FaPolicies.AdminOnly`
authorization policy (`AddAuthCore` registers it, requiring the configured
`AdminRoleName` role) rather than the self-or-admin check `getuserbyemail` uses — there's
no "self" case for browsing all users or creating one. `admincreateuser` mirrors
`register`: it creates the account *and* sends the confirmation/first-login email in one
call, generating a temporary password that's discarded in favor of the emailed
password-reset link — callers never see or need it. Note that role claims are baked into
a JWT at login time, so promoting a user to the admin role doesn't retroactively grant
access to a token they already hold — they need to log in again.

Three capabilities layer on top of core as pure enhancements — none are their own
module, and core works fine with none registered:

**Optional activation gate.** `login` normally succeeds for any valid credentials. To
make it reject inactive users too, register an `IActivationPolicy<TUser>` — the
built-in `AddDefaultActivationPolicy<TUser>()` (requires `IActivatableUser`, a plain
`bool IsActive`) or your own for anything more complex. `login` honors whichever
policy is registered, or works the same with none at all.

**`IAdminProvisionableUser` enhances two endpoints.** If `TUser` implements it,
`resetpassword`/`sendemailconfirmation` handle the admin-created-account first-login
flow automatically. If not, both endpoints still work — just without that extra
behavior.

**`IRoleAwareUser` enhances the admin lookup/list endpoints.** `Jwt2FaUserProjector<TUser>`
is synchronous and can't call `UserManager.GetRolesAsync` itself, so `getuserbyid` and
`listusers` populate `TUser`'s `Roles` property before projecting, if `TUser` implements
`IRoleAwareUser` — letting your projector include roles in the response without the
package needing to know your response shape. If `TUser` doesn't implement it, both
endpoints still work, just without roles populated.

One thing worth knowing about `login2fa`: it lives in `Add2Fa`, not core, even though
`login` (which reports a second factor is required) is core. Nothing else in the
package can ever put a user into a 2FA-required state, so completing one is `Add2Fa`'s
job specifically.

## Usage

```csharp
public class AppUser : IdentityUser, IActivatableUser, IAdminProvisionableUser, IMultiFactorMethodUser, IRoleAwareUser
{
    public bool IsActive { get; set; } = true;
    public bool HasSetPassword { get; set; }
    public string? TwoFactorMethod { get; set; }
    public List<string> Roles { get; set; } = new();
}
```

```csharp
builder.Services
    .AddIdentity<AppUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthCore<AppUser>(builder.Configuration, user => new
{
    user.Id,
    user.Email,
    user.TwoFactorEnabled,
    user.Roles
});
builder.Services.AddDefaultActivationPolicy<AppUser>();
builder.Services.Add2Fa<AppUser>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthCore<AppUser>();
app.Map2Fa<AppUser>();

app.Run();
```

Every `MapXyz` call takes an optional route prefix (default `"/auth"`), so both
modules land under one consistent route tree if you use both.

The `AddAuthCore` projector delegate (`Func<TUser, object>`) controls what gets
embedded in the JWT's `"user"` claim and returned from auth responses — project down
to whatever's safe to hand the client, never the raw `TUser` (password hash,
security stamp, etc.).

Every issued JWT also carries a standard `ClaimTypes.Role` claim for each of the
user's roles, so `[Authorize(Roles = "YourRole")]` (or a `RequireRole` policy) works
out of the box on your *own* app's endpoints and controllers — not just this
package's. `Jwt2FaPolicies.AdminOnly`, which the admin endpoints use, is built the
same way.

### Configuration

```json
{
  "Jwt2FaConfig": {
    "SecurityKey": "at-least-32-bytes-of-random-secret-here",
    "ValidIssuer": "your-app",
    "ValidAudience": "your-app-client",
    "ExpiryInMinutes": 60,
    "AdminRoleName": "Admin"
  },
  "Jwt2FaAuthCoreConfig": {
    "ApplicationName": "Your App",
    "FrontendBaseUrl": "https://your-app.example.com",
    "EmailConfirmationPath": "/email-confirmation?userId={userId}&code={code}",
    "ForgotPasswordPath": "/forgot-password/reset?code={code}"
  }
}
```

`AdminRoleName` is the Identity role treated as "admin" — for the self-or-admin checks
on endpoints like `getuserbyemail`, and for the `Jwt2FaPolicies.AdminOnly` policy the
admin-only endpoints require — defaults to `"Admin"`, override if your app names it
differently. `FrontendBaseUrl`
(no trailing slash) is combined with the two path templates to build the links
emailed to users — only the domain is configured once; the paths (and their
`{userId}`/`{code}` tokens) can point at whatever routes your frontend actually uses.

## License

MIT — see [LICENSE](https://github.com/dgates82/DGates.Identity.Jwt2Fa/blob/main/LICENSE).
