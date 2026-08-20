using DGates.Identity.Jwt2Fa.Dtos;
using DGates.Identity.Jwt2Fa.Extensions;
using DGates.Identity.Jwt2Fa.Jwt;
using DGates.Identity.Jwt2Fa.Tests.Fixtures;
using DGates.Identity.NotificationProviders.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace DGates.Identity.Jwt2Fa.Tests.Integration;

/// <summary>
/// Builds a real ASP.NET Core pipeline (routing, JWT bearer auth, both Jwt2Fa
/// modules) over a SQLite in-memory Identity store — a fresh instance per test class
/// via xUnit's <see cref="IAsyncLifetime"/>, since xUnit creates a new test class
/// instance per <c>[Fact]</c> by default, giving every test its own isolated database.
/// SQLite (not EF Core's InMemory provider) so real SQL — unique constraints, actual
/// generated queries — is exercised; the in-memory provider is known to diverge from
/// real relational behavior. The connection is opened once and kept alive for the
/// fixture's lifetime, since a SQLite in-memory database disappears when its one
/// connection closes.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private IHost _host = null!;

    protected HttpClient Client { get; private set; } = null!;
    protected FakeEmailSender EmailSender { get; private set; } = null!;
    protected FakeSmsSender SmsSender { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt2FaConfig:SecurityKey"] = "integration-test-security-key-please-1234",
                ["Jwt2FaConfig:ValidIssuer"] = "test-issuer",
                ["Jwt2FaConfig:ValidAudience"] = "test-audience",
                ["Jwt2FaConfig:ExpiryInMinutes"] = "30",
                ["Jwt2FaConfig:AdminRoleName"] = "Admin",
                ["Jwt2FaAuthCoreConfig:ApplicationName"] = "Test App",
                ["Jwt2FaAuthCoreConfig:FrontendBaseUrl"] = "https://app.example.com",
                ["Jwt2FaAuthCoreConfig:EmailConfirmationPath"] = "/email-confirmation?userId={userId}&code={code}",
                ["Jwt2FaAuthCoreConfig:ForgotPasswordPath"] = "/forgot-password/reset?userId={userId}&code={code}"
            })
            .Build();

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthorization();

                    services.AddDbContext<TestDbContext>(options => options.UseSqlite(_connection));

                    services.AddIdentity<TestUser, IdentityRole>(options =>
                    {
                        options.SignIn.RequireConfirmedAccount = true;
                        options.Password.RequireNonAlphanumeric = false;
                        options.Password.RequireUppercase = false;
                        options.Password.RequiredLength = 6;
                    })
                        .AddEntityFrameworkStores<TestDbContext>()
                        .AddDefaultTokenProviders();

                    var emailSender = new FakeEmailSender();
                    var smsSender = new FakeSmsSender();
                    services.AddSingleton(emailSender);
                    services.AddSingleton<IEmailSender>(emailSender);
                    services.AddSingleton(smsSender);
                    services.AddSingleton<ISmsSender>(smsSender);

                    Jwt2FaUserProjector<TestUser> projector = user => new { user.Id, user.Email };

                    services.AddAuthCore(configuration, projector);
                    services.AddDefaultActivationPolicy<TestUser>();
                    services.Add2Fa<TestUser>();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapAuthCore<TestUser>();
                        endpoints.Map2Fa<TestUser>();
                        endpoints.MapDefaultActivationPolicy<TestUser>();
                    });
                });
            });

        _host = await hostBuilder.StartAsync();

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        EmailSender = _host.Services.GetRequiredService<FakeEmailSender>();
        SmsSender = _host.Services.GetRequiredService<FakeSmsSender>();
        Client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
        await _connection.DisposeAsync();
    }

    /// <summary>Registers, confirms, and logs in a fresh user, returning the issued JWT.</summary>
    protected async Task<string> RegisterConfirmAndLoginAsync(string email, string password)
    {
        await Client.PostAsJsonAsync("/auth/register", new RegisterRequestDto { Email = email, Password = password });

        var confirmationEmail = EmailSender.SentEmails.Single(e => e.Email == email);
        var userId = EmailParsingHelper.ExtractQueryParam(confirmationEmail.HtmlMessage, "userId");
        var code = EmailParsingHelper.ExtractQueryParam(confirmationEmail.HtmlMessage, "code");
        await Client.PostAsJsonAsync("/auth/confirmemail", new ConfirmEmailRequestDto { UserId = userId, Code = code });

        return await LoginAsync(email, password);
    }

    /// <summary>Logs in an already-confirmed user, returning the issued JWT.</summary>
    protected async Task<string> LoginAsync(string email, string password)
    {
        var loginResponse = await Client.PostAsJsonAsync("/auth/login", new AuthRequestDto { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        return loginResult!.Token!;
    }

    /// <summary>
    /// Adds a user to a role directly via <see cref="RoleManager{TRole}"/>/<see cref="UserManager{TUser}"/>,
    /// bypassing HTTP — a shortcut for getting a role in place before it matters for a test
    /// (e.g. promoting the test's own admin caller before exercising admin endpoints),
    /// distinct from exercising <c>adminupdateuser</c>'s own role-diffing itself. Creates
    /// the role first if it doesn't already exist. A user with an already-issued JWT needs
    /// to log in again afterward to get a token carrying the new role claim.
    /// </summary>
    protected async Task AddToRoleAsync(string email, string role)
    {
        using var scope = _host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();

        await EnsureRoleExistsAsync(role);

        var user = await userManager.FindByEmailAsync(email);
        await userManager.AddToRoleAsync(user!, role);
    }

    /// <summary>
    /// Creates a role via <see cref="RoleManager{TRole}"/> if it doesn't already exist,
    /// without assigning it to anyone — for tests that need a role to exist before
    /// <c>adminupdateuser</c> can assign it via HTTP (Identity requires roles to
    /// pre-exist; it won't create one on the fly the way <see cref="AddToRoleAsync"/> does
    /// as a test convenience).
    /// </summary>
    protected async Task EnsureRoleExistsAsync(string role)
    {
        using var scope = _host.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    /// <summary>
    /// Puts a user into a locked-out state directly via <see cref="UserManager{TUser}"/>,
    /// bypassing HTTP — this package's own <c>login</c> never locks anyone out
    /// (<c>lockoutOnFailure: false</c>), so there's no HTTP path to reach this state to
    /// set up an <c>unlock</c> test.
    /// </summary>
    protected async Task LockOutUserAsync(string email)
    {
        using var scope = _host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();

        var user = await userManager.FindByEmailAsync(email);
        await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(30));
    }

    protected async Task<bool> IsLockedOutAsync(string email)
    {
        using var scope = _host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();

        var user = await userManager.FindByEmailAsync(email);
        return await userManager.IsLockedOutAsync(user!);
    }

    protected static HttpRequestMessage AuthorizedRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    protected static HttpRequestMessage WithJsonBody<T>(HttpRequestMessage request, T body)
    {
        request.Content = JsonContent.Create(body);
        return request;
    }
}
