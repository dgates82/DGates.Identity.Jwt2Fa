# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Ported `JwtHandler`/`AccountController` from `angular-dotnet-auth-template`, genericized
  over `TUser : IdentityUser` per
  [angular-dotnet-auth-template#8](https://github.com/dgates82/angular-dotnet-auth-template/issues/8)'s
  design: four opt-in modules (`AddAuthCore`/`AddAccountActivation`/`AddAdminProvisioning`/`Add2Fa`,
  each with a matching `MapXyz` for endpoint mapping) built around capability interfaces
  (`IActivatableUser`, `IAdminProvisionableUser`, `IMultiFactorMethodUser`) instead of one
  fixed user type
- `Jwt2FaUserProjector<TUser>` delegate — consumer-supplied projection embedded in the JWT's
  `"user"` claim and returned from auth responses, replacing the source app's hardcoded DTO
- `Jwt2FaResult<T>` — shared result wrapper letting services signal an HTTP outcome
  (Ok/BadRequest/NotFound/Unauthorized) without depending on ASP.NET Core's `IResult`
- All business logic lives in `Services/` (one interface + implementation per module),
  `ClaimsPrincipal`-based rather than `HttpContext`-based, so every service is directly usable
  outside this package's own endpoint-mapping layer
- 64 unit tests (mocked `UserManager`/`SignInManager`) and 8 integration tests (real HTTP via
  `Microsoft.AspNetCore.TestHost`, SQLite in-memory — not EF Core's `InMemory` provider)
  covering registration, login, both 2FA enrollment paths (TOTP authenticator and
  email-delivered codes), and the forgot-password/reset flow end to end

### Changed
- Renamed from the `dotnet-nuget-release-template` scaffold's `ExampleLibrary` to
  `DGates.Identity.Jwt2Fa`
- Single-targeted `net10.0` (dropped `net48` and the CI Mono step that supported it)
- CI/release workflows use real `vX.Y.Z` tags, not the template's `template-vX.Y.Z` scheme

### Removed
- The template's example Notes/S3 sample code and its `AWSSDK.S3` dependency
- The template's LocalStack/S3 CI step, `docker-compose.yml`, and `docker/seed.sh` — this
  package's own tests need no external services or containers

<!--
## [X.Y.Z] - YYYY-MM-DD

### Added
-

### Changed
-

### Fixed
-
-->
