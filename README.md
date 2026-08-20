# DGates.Identity.Jwt2Fa

[![CI](https://github.com/dgates82/DGates.Identity.Jwt2Fa/actions/workflows/ci.yml/badge.svg)](https://github.com/dgates82/DGates.Identity.Jwt2Fa/actions/workflows/ci.yml)

Real, claims-bearing JWTs and multi-channel two-factor authentication
(Authenticator/TOTP, Email, SMS) for ASP.NET Core Identity — generic over your own
user type.

Targets **.NET 10 only** — not compatible with .NET Framework (e.g. net48).

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

## Usage

### Minimal setup

Everything beyond bare `IdentityUser` is opt-in — this gets you register, login,
secure, and the whole password/email lifecycle, with no custom user type at all:

```csharp
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<YourDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthCore<IdentityUser>(builder.Configuration, user => new
{
    user.Id,
    user.Email
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthCore<IdentityUser>();

app.Run();
```

### Full setup

Two-factor auth, an activation gate, admin-created accounts, and role membership in
responses each need `TUser` to implement one small interface — nothing you don't use:

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
app.MapDefaultActivationPolicy<AppUser>();

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

## Configuration

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
    "ForgotPasswordPath": "/forgot-password/reset?userId={userId}&code={code}",
    "MaxPageSize": 100,
    "EmailConfirmationEmailSubject": "{applicationName} Email Confirmation",
    "EmailConfirmationEmailBody": "In order to start using {applicationName}, you need to verify your email.<br/><br/>Please confirm your account by <a href='{link}'>clicking here</a>.<br/><br/>If you did not request a login to {applicationName}, please ignore this email.",
    "AccountSetupEmailSubject": "{applicationName} Account Created",
    "AccountSetupEmailBody": "An account has been created for you on {applicationName}.<br/><br/>Please confirm your account and set your password by <a href='{link}'>clicking here</a>.<br/><br/>If you were not expecting this, please ignore this email.",
    "ForgotPasswordEmailSubject": "{applicationName} Password Reset",
    "ForgotPasswordEmailBody": "Forgot your password?<br/>We received a request to reset the password for your account.<br/><br/>To reset your password <a href='{link}'>click here</a>.<br/><br/>If you did not request a password reset please ignore this email.",
    "TwoFactorCodeEmailSubject": "{applicationName} 2FA Code",
    "TwoFactorCodeEmailBody": "Your 2FA code is: {code}<br/><br/>If you did not request a 2FA code please ignore this email.",
    "TwoFactorCodeSmsBody": "Your 2FA code for {applicationName} is: {code}. DO NOT share it with anyone."
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
Both templates identify the account by `{userId}`, not email — `resetpassword` looks
the user up by id, so your reset/setup page never needs to display, collect, or
transmit an email address; the reset code is already cryptographically bound to a
specific user.
`MaxPageSize` (optional, defaults to 100) is the hard cap `listusers` clamps its
`pageSize` query parameter to.

Every email/SMS this package sends has an overridable subject/body too — the six
`*Subject`/`*Body` properties above, all optional and already defaulted to the wording
shown (so leaving them out changes nothing). Each is a plain string with its own set of
`{token}` placeholders, substituted the same way `EmailConfirmationPath`/
`ForgotPasswordPath` are — `{applicationName}` and `{link}` on the email
subjects/bodies, `{code}` on the 2FA ones. Override just the ones you need; no
templating engine, no conditionals inside a single string — `AccountSetupEmailBody` is
sent both by `admincreateuser` and by `forgotpassword` reissuing a first-login link
(see below), so retext it once to cover both. `adminupdateuser`'s distinct
email-changed notice isn't independently configurable yet.

## Design: modules & capabilities

Everything is generic over `TUser : IdentityUser`. Each module is opt-in and each
capability beyond bare `IdentityUser` is its own small interface — skip a module,
never see its interface; opt into one without the matching interface, and it's a
compile error, not a silent no-op.

| Module | Registration | Requires on `TUser` | Endpoints |
|---|---|---|---|
| Core | `AddAuthCore<TUser>()` + `MapAuthCore<TUser>()` | nothing extra (optionally honors `IActivationPolicy<TUser>` — see below) | register, login, secure, getuserbyemail, getuserbyid, listusers, admincreateuser, adminupdateuser, unlock, forgotpassword, resetpassword, changepassword, sendemailconfirmation, confirmemail |
| Two-factor | `Add2Fa<TUser>()` + `Map2Fa<TUser>()` | `IMultiFactorMethodUser` | login2fa, sendtwofacode, enableauthenticator, verifyauthenticator, resetauthenticator |

**Admin endpoints** (`getuserbyid`, `listusers`, `admincreateuser`, `adminupdateuser`,
`unlock`) require the `Jwt2FaPolicies.AdminOnly` policy — `AddAuthCore` registers it
from the configured `AdminRoleName`, rather than the self-or-admin check
`getuserbyemail` uses (there's no "self" case for browsing all users or creating one).
Worth knowing:
- `admincreateuser` mirrors `register` — it creates the account *and* sends the
  confirmation/first-login email in one call. The generated temporary password is
  discarded in favor of the emailed reset link; callers never see or need it.
- `forgotpassword`, for an account that's confirmed its email but never set its own
  password (an `IAdminProvisionableUser` still at `HasSetPassword: false`), reissues
  that same first-login email rather than a generic password-reset one — lets you
  build a "resend setup link" action for an admin-created account whose original
  link went stale, without a separate endpoint.
- `adminupdateuser` only touches identity concerns this package knows about — email
  and role membership (a full-set diff against current roles, not a delta) — never
  app-specific profile fields, which stay on your own update endpoint to avoid
  mass-assignment risk. Changing the email re-sends a confirmation link, since
  Identity resets `EmailConfirmed` on any email change.
- `unlock` clears a locked-out user (`LockoutEnd` set to now) without resetting their
  failed-attempt count — requires nothing beyond `IdentityUser`, since lockout is a
  base Identity concept, not a capability interface.
- `listusers`' `pageSize` is clamped to `AuthCoreOptions.MaxPageSize` (default 100)
  regardless of what's requested — there's no "give me everyone" escape hatch.
- Role claims are baked into a JWT at login time, so promoting a user to admin (or
  editing roles via `adminupdateuser`) doesn't retroactively affect a token already
  issued — they need to log in again.

**Three capabilities layer on top of core as pure enhancements** — none are their own
module, and core works fine with none registered:
- **Optional activation gate.** Register an `IActivationPolicy<TUser>` — the built-in
  `AddDefaultActivationPolicy<TUser>()` (requires `IActivatableUser`, a plain
  `bool IsActive`) or your own for anything more complex — to make `login` reject
  inactive users; `login` works the same with none registered.
  `AddDefaultActivationPolicy<TUser>()` also registers `IUserActivationService<TUser>`;
  pair it with `MapDefaultActivationPolicy<TUser>()` for `activate`/`deactivate` admin
  endpoints that toggle `IsActive` directly. Both are scoped to the single-flag case —
  a custom, non-boolean `IActivationPolicy<TUser>` doesn't get these endpoints (the
  package can't know how to "activate" arbitrary logic) and should add its own.
- **`IAdminProvisionableUser`** enhances two endpoints: if `TUser` implements it,
  `resetpassword`/`sendemailconfirmation` handle the admin-created-account
  first-login flow automatically. If not, both still work, just without it.
- **`IRoleAwareUser`** enhances the admin lookup/list endpoints: `Jwt2FaUserProjector<TUser>`
  is synchronous and can't call `UserManager.GetRolesAsync` itself, so `getuserbyid`/
  `listusers` populate `TUser.Roles` before projecting, if implemented. If not, both
  still work, just without roles populated.

One thing worth knowing about `login2fa`: it lives in `Add2Fa`, not core, even though
`login` (which reports a second factor is required) is core. Nothing else in the
package can ever put a user into a 2FA-required state, so completing one is `Add2Fa`'s
job specifically.

Every `MapXyz` route group carries two endpoint filters automatically, requiring no
setup: an unhandled exception in any endpoint is logged and turned into a generic 500
message rather than reaching the client, and request DTOs are validated
(`System.ComponentModel.DataAnnotations`) with a 400 `ValidationProblem` on invalid
input rather than whatever a malformed request happens to do further down.

## License

MIT — see [LICENSE](https://github.com/dgates82/DGates.Identity.Jwt2Fa/blob/main/LICENSE).
