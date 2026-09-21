# DGates.Identity.Jwt2Fa

[![CI](https://github.com/dgates82/DGates.Identity.Jwt2Fa/actions/workflows/ci.yml/badge.svg)](https://github.com/dgates82/DGates.Identity.Jwt2Fa/actions/workflows/ci.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=dgates_identity-jwt2fa&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=dgates_identity-jwt2fa)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=dgates_identity-jwt2fa&metric=coverage)](https://sonarcloud.io/summary/new_code?id=dgates_identity-jwt2fa)
[![CodeQL](https://github.com/dgates82/DGates.Identity.Jwt2Fa/actions/workflows/codeql.yml/badge.svg)](https://github.com/dgates82/DGates.Identity.Jwt2Fa/actions/workflows/codeql.yml)
[![NuGet](https://img.shields.io/nuget/v/DGates.Identity.Jwt2Fa.svg)](https://www.nuget.org/packages/DGates.Identity.Jwt2Fa)

Real, claims-bearing JWTs and multi-channel two-factor authentication
(Authenticator/TOTP, Email, SMS) for ASP.NET Core Identity — generic over your own
user type.

Targets **.NET 10 only** — not compatible with .NET Framework (e.g. net48).

## See it running

[angular-dotnet-auth-template](https://github.com/dgates82/angular-dotnet-auth-template)
is a full Angular + .NET app built on this package, with a live demo currently
running **v1.1.0**. Register an account and try authenticator/TOTP, email, and SMS
2FA. No real email or SMS is sent — messages land in the public mock inboxes.

- [Live demo](https://angular-dotnet-auth-template-1019453023791.us-central1.run.app)
- [SendGrid mock](https://sendgrid-mock-7qs7btajdq-uc.a.run.app) (email inbox)
- [Twilio mock](https://twilio-mock-1019453023791.us-central1.run.app) (SMS inbox)

## What you get

- Real, signed JWTs with a projected user claim and role claims — not opaque,
  Data-Protection-encrypted tokens
- `register`/`login`/`secure` and the full password and email-confirmation lifecycle
- Admin user management: list, get, create, update, unlock
- Multi-channel 2FA: authenticator app (TOTP), email codes, SMS codes
- Generic over your own `TUser : IdentityUser`, with opt-in capability interfaces —
  no forced base class beyond Identity's own
- An optional activation gate (`login` rejects inactive users)
- Every endpoint gets a generic-500 safety net and DTO validation automatically

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

[`DGates.Identity.NotificationProviders`](https://github.com/dgates82/DGates.Identity.NotificationProviders)
is a real, shipped dependency — installing this package already brings it in, you
don't add it separately. It supplies the `ISmsSender` interface and concrete senders
(SendGrid/SMTP/Postmark for email, Twilio/AWS SNS for SMS), but none are wired up
automatically: registration and 2FA still need an `IEmailSender`
(`Microsoft.AspNetCore.Identity.UI.Services.IEmailSender`) and, for SMS 2FA, an
`ISmsSender` explicitly registered — call one of `NotificationProviders`'
`AddXyzEmailSender()`/`AddXyzSmsSender()` extensions, or bring your own.

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
modules land under one route tree. The `AddAuthCore` projector delegate
(`Func<TUser, object>`) controls what's embedded in the JWT's `"user"` claim and
returned from auth responses — project down to what's safe to hand the client,
never the raw `TUser`. Every issued JWT also carries a standard `ClaimTypes.Role`
claim per role, so `[Authorize(Roles = "YourRole")]` works out of the box on your
*own* endpoints too.

## A login, concretely

`POST /auth/login` for a user in the `Admin` role, with the Full setup's projector
above. Real output, fake data — `email`/`password` in, a 200 with a `user` object and
a real signed JWT out:

```json
{
  "isAuthSuccessful": true,
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "8ed87255-2dc6-478e-a413-57cf70e3a769",
    "email": "jane.doe@example.com",
    "twoFactorEnabled": false,
    "roles": ["Admin"]
  }
}
```

The token's decoded payload — readable claims, not an opaque reference:

```json
{
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "8ed87255-2dc6-478e-a413-57cf70e3a769",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": "jane.doe@example.com",
  "user": { "Id": "8ed87255...", "Email": "jane.doe@example.com", "TwoFactorEnabled": false, "Roles": ["Admin"] },
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Admin",
  "exp": 1789992073,
  "iss": "your-app",
  "aud": "your-app-client"
}
```

The `"user"` claim is whatever the projector returns; the separate role claim is
what `[Authorize(Roles = "Admin")]` actually checks, issued independently of it.

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
    "ForgotPasswordPath": "/forgot-password/reset?userId={userId}&code={code}"
  }
}
```

`FrontendBaseUrl` (no trailing slash) is combined with the two path templates to
build the links emailed to users — only the domain is configured once. See
[Configuration](https://github.com/dgates82/DGates.Identity.Jwt2Fa/blob/main/docs/CONFIGURATION.md)
for `AdminRoleName`, `MaxPageSize`, and overriding email/SMS copy.

## Design: modules & capabilities

Two opt-in modules (`AddAuthCore`/`MapAuthCore` and `Add2Fa`/`Map2Fa`) and four
capability interfaces that opportunistically enhance core if your `TUser`
implements them, with no loss of functionality if it doesn't. Skip a module, never
see its interface; opt into one without the matching interface, and it's a compile
error, not a silent no-op.

See [Design](https://github.com/dgates82/DGates.Identity.Jwt2Fa/blob/main/docs/DESIGN.md)
for the full module table, admin endpoint behavior, and the three capability
interfaces.

## Part of a small ecosystem

| Project | What it is | Reach for it when |
| --- | --- | --- |
| **DGates.Identity.Jwt2Fa** (you are here) | JWT issuance and multi-channel 2FA for ASP.NET Core Identity | you want real JWTs and TOTP/email/SMS 2FA on your own API |
| [DGates.Identity.NotificationProviders](https://github.com/dgates82/DGates.Identity.NotificationProviders) ([NuGet](https://www.nuget.org/packages/DGates.Identity.NotificationProviders)) | Email and SMS senders (SendGrid, SMTP, Postmark, Twilio, SNS) | you need swappable notification providers |
| [angular-dotnet-auth-template](https://github.com/dgates82/angular-dotnet-auth-template) | Angular 21 + .NET 10 starter with this package wired in, live demo, Cloud Run pipeline | you want a running app, not just the library |
| [dgates-mock-servers](https://github.com/dgates82/dgates-mock-servers) | Public GHCR images mocking SendGrid, Twilio, and Postmark | you want to develop or test notification flows with no accounts |

angular-dotnet-auth-template → DGates.Identity.Jwt2Fa → DGates.Identity.NotificationProviders → dgates-mock-servers (in dev)

This package pins `DGates.Identity.NotificationProviders` to a specific version
(currently `1.1.0`), not "latest" — that pin will lag again whenever
`NotificationProviders` releases next.

More from dgates82: [DGates.AwsSecretsManager](https://github.com/dgates82/DGates.AwsSecretsManager)
and [dotnet-nuget-release-template](https://github.com/dgates82/dotnet-nuget-release-template),
the template this package was scaffolded from.

## License

MIT — see [LICENSE](https://github.com/dgates82/DGates.Identity.Jwt2Fa/blob/main/LICENSE).
