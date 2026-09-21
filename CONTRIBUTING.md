# Contributing

Thanks for your interest in improving this package. A few guidelines to keep things consistent.

## Before you start

- For anything beyond a small fix, open an issue first to discuss the change.
- The design (opt-in modules, capability interfaces instead of one big user
  contract) is deliberate — see [docs/DESIGN.md](docs/DESIGN.md)
  and [angular-dotnet-auth-template#8](https://github.com/dgates82/angular-dotnet-auth-template/issues/8)
  before proposing a new capability interface. Resist folding things back into one
  big `IAppUser` — that's the shape this design specifically avoids.

## Development setup

- `dotnet restore`
- `dotnet test` — runs everything, no Docker or external services required. Unit
  tests mock `UserManager`/`SignInManager` directly; integration tests run real HTTP
  through `Microsoft.AspNetCore.TestHost` against a SQLite in-memory database (not
  EF Core's `InMemory` provider — that doesn't enforce unique constraints or exercise
  real generated SQL) with fake in-memory email/SMS senders.

## Making changes

- Branch from `main`, open a PR — direct pushes to `main` are blocked.
- Commit format: `type: lowercase description` (e.g. `feat:`, `fix:`, `docs:`, `chore:`).
- A new module needs three things, not just an endpoint: the capability interface
  (if it needs one), an `AddXyz<TUser>()`/`MapXyz<TUser>()` pair mirroring the
  existing modules' shape, and the business logic living in `Services/` (an
  interface + implementation), not inline in the endpoint mapping — endpoint methods
  should stay thin wrappers that extract the request DTO and `HttpContext.User`,
  call the service, and return `.ToIResult()`.
- Services take `ClaimsPrincipal`, never `HttpContext`, for "who is the caller" —
  keeps them usable directly (e.g. from a hand-rolled controller) without any
  dependency on this package's own endpoint-mapping layer.
- If a module's DI registration method and its endpoint-mapping method end up
  needing different generic constraints (this happened with account activation —
  `getuserbyemail` doesn't need `IActivationPolicy<TUser>` even though the module's
  *optional* default-policy convenience does), split into two methods rather than
  loosening the constraint via a runtime type check. A runtime check would silently
  no-op instead of failing to compile, which breaks the "wrong shape → compile
  error" guarantee every other module relies on.
- Update `CHANGELOG.md` under `[Unreleased]` for any user-facing change.

## Pull requests

- 1 approval required before merge (GitHub Ruleset on `main`).
- CI must pass — build, unit tests, and integration tests.
- Merge via merge commit, not squash — keeps full commit history intact.

## Releasing

This package uses real `vX.Y.Z` tags (e.g. `v1.0.0`), which `release.yml` watches
for directly.

1. Push the tag explicitly: `git tag vX.Y.Z <sha> && git push origin vX.Y.Z`.
2. Let `release.yml` fire, publishing to NuGet.org via Trusted Publishing (OIDC) —
   no API key is stored in the repo.
3. Confirm the workflow run is green.
4. Create the GitHub Release by selecting the tag that already exists — never type
   a new tag name into the release form.

## What not to contribute

- API keys, secrets, or anything that would require moving off Trusted Publishing.
- A hardcoded role name for the self-or-admin authorization checks — that's what
  `Jwt2FaConfig:AdminRoleName` is for.
- A hard dependency on a specific `IEmailSender`/`ISmsSender` implementation. This
  package consumes those interfaces; it doesn't ship or assume any particular
  provider.

## Questions

Open an issue if you're not sure whether something fits.
