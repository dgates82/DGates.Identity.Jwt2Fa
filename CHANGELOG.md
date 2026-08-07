# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Ported `JwtHandler`/`AccountController` from `angular-dotnet-auth-template`, genericized
  over `TUser : IdentityUser` per
  [angular-dotnet-auth-template#8](https://github.com/dgates82/angular-dotnet-auth-template/issues/8)'s
  design, built around capability interfaces instead of one fixed user type: two opt-in
  modules, each an `AddXyz`/`MapXyz` pair —
  - `AddAuthCore`/`MapAuthCore` — register, login, secure, the password/email lifecycle
    (forgotpassword/resetpassword/changepassword/sendemailconfirmation/confirmEmail),
    account lookup (getuserbyemail), and admin user management (getuserbyid, listusers,
    admincreateuser, adminupdateuser). Requires nothing beyond `IdentityUser`.
  - `Add2Fa`/`Map2Fa` — login2fa, sendtwofacode, enableauthenticator,
    verifyauthenticator, resetauthenticator. Requires `IMultiFactorMethodUser`.
- Four capability interfaces enhancing core opportunistically — implement one, get the
  matching behavior; skip it, core still works without it: `IActivatableUser` (an
  `IsActive` flag `login` can honor via `AddDefaultActivationPolicy`, and the
  `activate`/`deactivate` admin endpoints via `MapDefaultActivationPolicy` can toggle —
  consumers with more than a single flag's worth of activation logic implement
  `IActivationPolicy<TUser>` directly instead and own their own admin action for it),
  `IAdminProvisionableUser` (first-login state for `resetpassword`/
  `sendemailconfirmation`/`admincreateuser`), `IMultiFactorMethodUser` (2FA channel
  selector, required by `Add2Fa`), `IRoleAwareUser` (lets `getuserbyid`/`listusers`
  populate role membership before projecting)
- `Jwt2FaPolicies.AdminOnly` authorization policy, built on the role claims every issued
  JWT already carries — also usable directly by consuming apps via
  `[Authorize(Roles = "YourRole")]`, not just this package's own endpoints
- Admin user-management endpoints `getuserbyid`, `listusers` (paginated, `pageSize`
  clamped to a configurable `MaxPageSize`), `admincreateuser` (atomic account creation +
  first-login email, one call instead of create-then-send-confirmation), `adminupdateuser`
  (email + role-membership diff; re-sends a confirmation email if the email changes,
  since `SetEmailAsync` resets `EmailConfirmed`) — `admincreateuser`/`adminupdateuser`
  bind to narrow, package-defined DTOs rather than `TUser` directly, to avoid
  mass-assignment risk
- `Jwt2FaUserProjector<TUser>` delegate — consumer-supplied projection embedded in the JWT's
  `"user"` claim and returned from auth responses, replacing the source app's hardcoded DTO
- `Jwt2FaResult<T>` — shared result wrapper letting services signal an HTTP outcome
  (Ok/BadRequest/NotFound/Unauthorized) without depending on ASP.NET Core's `IResult`
- All business logic lives in `Services/` (one interface + implementation per module),
  `ClaimsPrincipal`-based rather than `HttpContext`-based, so every service is directly usable
  outside this package's own endpoint-mapping layer
- `SonarAnalyzer.CSharp` static analysis (build-time Roslyn analyzer only; SonarQube
  Cloud/CI integration deferred until this repo is public)
- 98 tests (unit tests against mocked `UserManager`/`SignInManager`, plus integration
  tests over a real ASP.NET Core pipeline backed by SQLite — not EF Core's `InMemory`
  provider) covering registration, login, both 2FA enrollment paths (TOTP authenticator
  and email-delivered codes), the forgot-password/reset flow, and the full admin-endpoint
  surface including role-claim staleness after a promotion

### Changed
- Renamed from the `dotnet-nuget-release-template` scaffold's `ExampleLibrary` to
  `DGates.Identity.Jwt2Fa`
- Single-targeted `net10.0` (dropped `net48` and the CI Mono step that supported it)
- CI/release workflows use real `vX.Y.Z` tags, not the template's `template-vX.Y.Z` scheme
- `login2fa` moved from core into `Add2Fa` — nothing else in the package can ever put a
  user into a 2FA-required state, so it was unreachable without `Add2Fa` regardless of
  where it lived
- `AuthCoreOptions` gained `FrontendBaseUrl`; callback URL options shrank to path-only
  templates (`EmailConfirmationPath`/`ForgotPasswordPath`) concatenated onto it, instead
  of each repeating the full domain

### Removed
- The template's example Notes/S3 sample code and its `AWSSDK.S3` dependency
- The template's LocalStack/S3 CI step, `docker-compose.yml`, and `docker/seed.sh` — this
  package's own tests need no external services or containers
- The originally-planned `AddAccountActivation`/`AddAdminProvisioning` modules — neither
  ended up gating anything its endpoints didn't already work fine without; their
  endpoints (`getuserbyemail`, and the password/email lifecycle) moved into core instead

### Fixed
- 2FA code delivery no longer claims a specific expiry window — Identity's built-in
  Email/Phone token providers use a fixed, private, unconfigurable 3-minute window
  internally, so the source app's "valid for N minutes" messaging was never true. A real,
  independently-configurable expiry is tracked as
  [#1](https://github.com/dgates82/DGates.Identity.Jwt2Fa/issues/1), deliberately not
  bundled into this release

<!--
## [X.Y.Z] - YYYY-MM-DD

### Added
-

### Changed
-

### Fixed
-
-->
