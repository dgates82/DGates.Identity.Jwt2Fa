# Configuration

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
    "MaxPageSize": 100
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

## Customizing email/SMS copy

Every email/SMS this package sends also has an overridable subject/body — six
`*Subject`/`*Body` properties on `Jwt2FaAuthCoreConfig`, all optional and already
defaulted to the wording shown below (so leaving them out changes nothing):

```json
{
  "Jwt2FaAuthCoreConfig": {
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

Each is a plain string with its own set of `{token}` placeholders, substituted the
same way `EmailConfirmationPath`/`ForgotPasswordPath` are — `{applicationName}` and
`{link}` on the email subjects/bodies, `{code}` on the 2FA ones. Override just the
ones you need; no templating engine, no conditionals inside a single string —
`AccountSetupEmailBody` is sent both by `admincreateuser` and by `forgotpassword`
reissuing a first-login link (see [Design](DESIGN.md)), so retext it once to cover
both. `adminupdateuser`'s distinct email-changed notice isn't independently
configurable yet.
