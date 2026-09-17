# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2026-09-17

### Added
- SonarQube Cloud static analysis, wired into CI's `build-and-test` job via
  `dotnet-sonarscanner` and gated on the quality gate result — a failing gate now fails
  the build. Results are posted directly to the PR (status checks and a summary comment),
  and a quality gate badge was added to the README.
- Test coverage reporting to SonarQube Cloud via `dotnet test --collect:"XPlat Code
  Coverage"` (coverlet.collector) fed into the scan via `sonar.cs.cobertura.reportsPaths`.

### Changed
- Project key/org moved from hardcoded literals to `SONAR_PROJECT_KEY`/`SONAR_ORG` repo
  variables.

### Fixed
- `workflow_dispatch` runs weren't picking up the branch name (a SonarScanner limitation),
  silently analyzing as if there were no branch at all — now passed explicitly for any
  non-PR trigger.
- `Register_WithWellFormedRequest_IsNotRejectedByValidation` now has an explicit assertion
  (SonarQube S2699) — it always failed correctly via `EnsureSuccessStatusCode()`, Sonar just
  didn't recognize that as a formal assertion.
- `TwoFactorService.SendTwoFaCodeAsync`'s per-provider send logic extracted into
  `TrySendTwoFaCodeAsync`, bringing cyclomatic complexity under the SonarQube threshold
  (S1541) — behavior-preserving, no logic change.
- Cleared 24 of the remaining 32 backlog SonarQube Cloud findings from issue #37: constant
  array literals in tests hoisted to shared static fields (`CA1861`), magic numbers named
  as constants (`S109`), two of three duplicated-string-literal groups extracted to
  constants (`S1192` — the third, in `TwoFactorService`, is deferred to land after this
  branch's own refactor of that same method to avoid touching the same lines twice), a
  long doc-comment line wrapped (`S103`), a test regex converted to `[GeneratedRegex]`
  (`SYSLIB1045`), authorization policy registration switched to `AddAuthorizationBuilder`
  (`ASP0025`), and `Assert.IsAssignableFrom` replaced with
  `Assert.IsType(..., exactMatch: false)` (`xUnit2032`).
- `AuthCoreService`'s three near-identical email-sending call sites
  (`SendAccountSetupEmailAsync`, `SendEmailConfirmationEmailAsync`, and
  `ForgotPasswordAsync`'s inline send) extracted into a shared `SendTemplatedEmailAsync`
  helper — the constant-extraction above pushed them over SonarQube Cloud's duplication
  threshold, so the actual duplication was removed instead of just tolerated
  (`new_duplicated_lines_density`). Behavior-preserving only.
- `TwoFactorService`'s repeated `"Phone"` literal extracted to a `PhoneMethodName`
  constant (`S1192`).

## [1.0.0] - 2026-08-22

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
- `Jwt2FaResult<T>` — moves business logic out of endpoint delegates and into
  injectable services that signal an HTTP outcome without depending on ASP.NET Core's
  `IResult`, so every service is usable (and testable) outside this package's own
  endpoint-mapping layer too
- Every email/SMS this package sends now has a consumer-overridable subject/body on
  `AuthCoreOptions` — email confirmation, account setup/first-login, forgot password,
  and both 2FA code channels (Email/SMS) — each a plain `{token}`-substituted string
  defaulting to the package's existing wording, so a consumer wanting different
  copy no longer has to fork the package to get it
  ([#23](https://github.com/dgates82/DGates.Identity.Jwt2Fa/issues/23))

### Changed
- **Breaking:** `ResetPasswordRequestDto.Email` is now `UserId` - `resetpassword`
  identifies the account by id, not email. `ForgotPasswordPath` gains a `{userId}`
  token (mirroring `EmailConfirmationPath`'s existing one); `EmailConfirmationPath`'s
  `{email}` token is removed, since its only purpose was pre-filling a reset-password
  form's email field, which no longer exists - the reset code is already
  cryptographically bound to a specific user, so a consuming reset/setup page never
  needs to display, collect, or transmit an email address at all
  ([#26](https://github.com/dgates82/DGates.Identity.Jwt2Fa/issues/26))

### Security
- `sendtwofacode` now sends Phone/SMS setup codes to the submitted phone number
  whenever the account isn't already verified for the Phone method specifically,
  instead of only when 2FA isn't enabled at all — switching from Email or
  Authenticator to Phone previously sent the code to the account's stored (empty)
  phone number and silently dropped it, so the switch could never complete. The
  self-or-admin authorization check now applies to that case too, matching the trust
  decision it's guarding. This check was fixed once already in the source app
  (an unauthenticated 2FA-takeover issue), and the port to this package dropped it
  again for this one branch, so it's called out separately from routine fixes below
  ([#16](https://github.com/dgates82/DGates.Identity.Jwt2Fa/issues/16))
- `changepassword` now requires the caller to be the target account or hold the
  configured admin role, matching every other identity-mutating endpoint
  (`getuserbyemail`, `sendtwofacode`, `enableauthenticator`, `verifyauthenticator`,
  `resetauthenticator`) — previously any authenticated user could change a different
  account's password given that account's current password, since the endpoint only
  required *some* valid JWT rather than the target's own
  ([#32](https://github.com/dgates82/DGates.Identity.Jwt2Fa/issues/32))

### Fixed
- `login`, `login2fa`, and `getuserbyemail` now populate `IRoleAwareUser.Roles` before
  projecting the response, matching `getuserbyid`/`listusers`/`admincreateuser` — role
  claims in the issued JWT were always correct, but a consumer reading roles off the
  embedded `user` object (rather than decoding the JWT) previously saw an empty list on
  these three endpoints ([#7](https://github.com/dgates82/DGates.Identity.Jwt2Fa/issues/7))
- Every mapped endpoint now has a baseline safety net: an unhandled exception is
  logged and turned into a generic 500 message instead of reaching the client, and
  request DTOs are validated with a 400 `ValidationProblem` on invalid input. The
  source app had both (a catch-all per action, `[ApiController]`'s automatic
  400-on-invalid-input) on every controller action; neither carried over automatically
  when the port moved to minimal APIs
  ([#12](https://github.com/dgates82/DGates.Identity.Jwt2Fa/issues/12))
- `register` now sets `IAdminProvisionableUser.HasSetPassword` to `true` after a
  successful `CreateAsync`, matching what `resetpassword` already does — a
  self-registered user chose their own password at signup, but nothing recorded that,
  so a consumer reading `HasSetPassword` (e.g. an admin dashboard) saw `false` forever
  for anyone who never happened to reset their password afterward
  ([#18](https://github.com/dgates82/DGates.Identity.Jwt2Fa/issues/18))
- `forgotpassword` now appends `&isFirstLogin=true` to its reset link for an account
  that's never set its own password (`IAdminProvisionableUser.HasSetPassword == false`),
  matching the framing `admincreateuser`'s own first-login email already carries — lets
  a consumer reissue a working first-login link for an admin-created account whose
  original one went stale, without losing that context
- `ForgotPasswordEmailBody`'s default wording had a single `<br/>` between its
  first two lines while every other line break in it (and in the other five
  default templates) used `<br/><br/>` for paragraph spacing, so the opening
  line rendered as a cramped run-on against the paragraph beneath it
- `forgotpassword` now sends the same account-setup email `admincreateuser` sends when
  reissuing a first-login link (`IAdminProvisionableUser.HasSetPassword == false`),
  instead of its own "forgot your password" wording with just the link patched — the
  link's `isFirstLogin=true` correctly framed the destination page, but the email
  itself still told the recipient "we received a request to reset your password...
  if you did not request this, ignore it," which is backwards when an admin is
  reissuing the link on their behalf
  ([#22](https://github.com/dgates82/DGates.Identity.Jwt2Fa/issues/22))

### Known limitations
- 2FA code expiry isn't independently configurable yet — tracked as
  [#1](https://github.com/dgates82/DGates.Identity.Jwt2Fa/issues/1)
- `adminupdateuser`'s email-changed notice isn't independently configurable yet
  (it reuses no dedicated `*Subject`/`*Body` pair)

<!--
## [X.Y.Z] - YYYY-MM-DD

### Added
-

### Changed
-

### Fixed
-
-->
