# DGates.Identity.Jwt2Fa — Local Development

This package's own tests need no Docker, containers, or external services — everything
runs in-process.

## Running tests

```sh
dotnet test
```

Runs both suites:

- **Unit tests** (`tests/DGates.Identity.Jwt2Fa.Tests/Services/`, `Jwt/`, `TwoFactor/`) — mock
  `UserManager`/`SignInManager` directly via `Fixtures/IdentityMockFactory.cs`. No database, no
  HTTP.
- **Integration tests** (`tests/DGates.Identity.Jwt2Fa.Tests/Integration/`) — real HTTP through
  `Microsoft.AspNetCore.TestHost`, backed by a SQLite in-memory database (one connection kept
  open for the test class's lifetime — see `IntegrationTestBase`), not EF Core's `InMemory`
  provider, since that doesn't enforce unique constraints or exercise real generated SQL. Email
  and SMS are captured in memory by `FakeEmailSender`/`FakeSmsSender` instead of sent anywhere,
  so the whole suite runs offline.

## Trying it against a real app

This package doesn't ship its own runnable app — to exercise it end to end, wire it into a
consuming project (see the [README](../README.md#usage) for the DI/endpoint wiring) with:

- A real `TUser`/`TContext` pair and a real database provider (SQL Server, PostgreSQL, MySQL,
  etc.) via `.AddEntityFrameworkStores<TContext>()`.
- A real `IEmailSender` — e.g.
  [`DGates.Identity.NotificationProviders`](https://github.com/dgates82/DGates.Identity.NotificationProviders)'
  `AddSmtpEmailSender()` pointed at a local Mailpit container, or any other implementation of
  `Microsoft.AspNetCore.Identity.UI.Services.IEmailSender`.
- An `ISmsSender` (same package's `AddTwilioSmsSender()`/`AddSnsSmsSender()`, or your own) only
  if you use `Add2Fa`'s SMS channel.

## Building a local NuGet package

Use this to test `dotnet pack`/versioning locally before relying on `release.yml`, or to
sanity-check the packed output before tagging a real release.

```sh
dotnet pack --configuration Release /p:Version=0.1.0 --output ./nupkg
```

To reference the local package from another project, add a local NuGet source:

```sh
dotnet nuget add source /path/to/this/repo/nupkg --name LocalJwt2FaTest
```

Then reference it normally in the consuming project's `.csproj`:

```xml
<PackageReference Include="DGates.Identity.Jwt2Fa" Version="0.1.0" />
```
