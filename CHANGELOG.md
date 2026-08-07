# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Real, claims-bearing JWTs and multi-channel two-factor authentication
  (Authenticator/TOTP, Email, SMS) for ASP.NET Core Identity, generic over your own
  `TUser : IdentityUser`
- Two opt-in modules: `AddAuthCore`/`MapAuthCore` (register, login, the password/email
  lifecycle, account lookup, and admin user management) and `Add2Fa`/`Map2Fa` (2FA
  enrollment and verification across TOTP, email, and SMS)
- Four capability interfaces that opportunistically enhance core if your `TUser`
  implements them, with no loss of functionality if it doesn't: `IActivatableUser`,
  `IAdminProvisionableUser`, `IMultiFactorMethodUser`, `IRoleAwareUser`
- Admin user-management endpoints (list/get/create/update users, activate/deactivate,
  unlock), gated by a new `Jwt2FaPolicies.AdminOnly` policy built on JWT role claims —
  also usable directly on your own app's endpoints via `[Authorize(Roles = "YourRole")]`
- `Jwt2FaUserProjector<TUser>` — controls what's embedded in the JWT and returned from
  auth responses, so nothing sensitive on `TUser` leaks by default
- `Jwt2FaResult<T>` — lets the service layer signal an HTTP outcome without depending on
  ASP.NET Core's `IResult`, so every service is usable outside this package's own
  endpoint-mapping layer too

### Known limitations
- 2FA code expiry isn't independently configurable yet — tracked as
  [#1](https://github.com/dgates82/DGates.Identity.Jwt2Fa/issues/1)

<!--
## [X.Y.Z] - YYYY-MM-DD

### Added
-

### Changed
-

### Fixed
-
-->
