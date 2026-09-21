# Design: modules & capabilities

Everything is generic over `TUser : IdentityUser`. Each module is opt-in and each
capability beyond bare `IdentityUser` is its own small interface — skip a module,
never see its interface; opt into one without the matching interface, and it's a
compile error, not a silent no-op.

Core also includes full user administration (list/get/create/update/unlock) —
extracted alongside login/JWT logic because the source app's admin and auth concerns
lived in the same two controllers (`AccountController`, `Admin/UserController`) and
needed the same `TUser` genericization; splitting them into separate packages would
have meant solving that problem twice.

| Module | Registration | Requires on `TUser` | Endpoints |
|---|---|---|---|
| Core | `AddAuthCore<TUser>()` + `MapAuthCore<TUser>()` | nothing extra (optionally honors `IActivationPolicy<TUser>` — see below) | register, login, secure, getuserbyemail, getuserbyid, listusers, admincreateuser, adminupdateuser, unlock, forgotpassword, resetpassword, changepassword, sendemailconfirmation, confirmemail |
| Two-factor | `Add2Fa<TUser>()` + `Map2Fa<TUser>()` | `IMultiFactorMethodUser` | login2fa, sendtwofacode, enableauthenticator, verifyauthenticator, resetauthenticator |

## Admin endpoints

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

## Capabilities

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

## Endpoint filters

Every `MapXyz` route group carries two endpoint filters automatically, requiring no
setup: an unhandled exception in any endpoint is logged and turned into a generic 500
message rather than reaching the client, and request DTOs are validated
(`System.ComponentModel.DataAnnotations`) with a 400 `ValidationProblem` on invalid
input rather than whatever a malformed request happens to do further down.
