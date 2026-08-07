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

You'll also need *some* implementation of `Microsoft.AspNetCore.Identity.UI.Services.IEmailSender`
(used by registration, password reset, and email 2FA) and, if you use SMS 2FA, an
`ISmsSender` from
[`DGates.Identity.NotificationProviders`](https://github.com/dgates82/DGates.Identity.NotificationProviders) —
this package doesn't ship notification delivery itself.

## Design: opt-in modules, capability interfaces instead of one big user contract

Everything is generic over `TUser : IdentityUser`, but the three pieces below aren't
one monolithic registration — each is opt-in, and each capability beyond bare
`IdentityUser` is its own small interface your `TUser` implements only if you use the
module that needs it. Skip a module, never see its interface. Opt into one without
the matching interface, and it's a compile error, not a silent no-op.

| Module | Registration | Requires on `TUser` | Endpoints |
|---|---|---|---|
| Core | `AddAuthCore<TUser>()` + `MapAuthCore<TUser>()` | nothing extra | register, login, secure, forgotpassword, resetpassword, changepassword, sendemailconfirmation, confirmEmail |
| Account activation | `AddAccountActivation<TUser>()` + `MapAccountActivation<TUser>()` | nothing extra (see below) | getuserbyemail |
| Two-factor | `Add2Fa<TUser>()` + `Map2Fa<TUser>()` | `IMultiFactorMethodUser` | login2fa, sendtwofacode, enableauthenticator, verifyauthenticator, resetauthenticator |

Two things worth calling out:

- **Account activation:** `getuserbyemail` doesn't itself need anything beyond
  `IdentityUser`, but if you also want the **core login endpoint** to reject inactive
  users, register an `IActivationPolicy<TUser>` — either the built-in default
  (`AddDefaultActivationPolicy<TUser>()`, if `TUser` implements `IActivatableUser`, a
  plain `bool IsActive` flag) or your own, for anything more than a single flag.
  Core's login honors whichever policy is registered, if any — it works identically
  with no policy registered at all.
- **`login2fa` lives in the two-factor module, not core**, even though `login` (the
  request that reports a second factor is required) is core. Nothing can ever put a
  user into a 2FA-required state without `Add2Fa`'s enroll/verify flow having enabled
  it first, so `login2fa` would be permanently unreachable without that module —
  keeping it there is what makes it work, not just where it happens to live.
- **`IAdminProvisionableUser`** (`bool HasSetPassword`) is an *optional* enhancement
  on top of core's password/email endpoints, not a capability any module hard-requires.
  If `TUser` implements it, `resetpassword` sets it and `sendemailconfirmation` bundles
  a first-login password-reset link for accounts that haven't set one yet (e.g.
  admin-created accounts). If `TUser` doesn't implement it, both endpoints work exactly
  the same, minus that enhancement — there's no separate module to opt into.

## Usage

```csharp
public class AppUser : IdentityUser, IActivatableUser, IAdminProvisionableUser, IMultiFactorMethodUser
{
    public bool IsActive { get; set; } = true;
    public bool HasSetPassword { get; set; }
    public string? TwoFactorMethod { get; set; }
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
    user.TwoFactorEnabled
});
builder.Services.AddAccountActivation<AppUser>();
builder.Services.AddDefaultActivationPolicy<AppUser>();
builder.Services.Add2Fa<AppUser>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthCore<AppUser>();
app.MapAccountActivation<AppUser>();
app.Map2Fa<AppUser>();

app.Run();
```

Every `MapXyz` call takes an optional route prefix (default `"/auth"`), so all three
modules land under one consistent route tree if you use all of them.

The `AddAuthCore` projector delegate (`Func<TUser, object>`) controls what gets
embedded in the JWT's `"user"` claim and returned from auth responses — project down
to whatever's safe to hand the client, never the raw `TUser` (password hash,
security stamp, etc.).

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

`AdminRoleName` is the Identity role treated as "admin" for the self-or-admin checks
used across several endpoints (e.g. an admin looking up another user's account) —
defaults to `"Admin"`, override if your app names it differently. `FrontendBaseUrl`
(no trailing slash) is combined with the two path templates to build the links
emailed to users — only the domain is configured once; the paths (and their
`{userId}`/`{code}` tokens) can point at whatever routes your frontend actually uses.

## License

MIT — see [LICENSE](https://github.com/dgates82/DGates.Identity.Jwt2Fa/blob/main/LICENSE).
