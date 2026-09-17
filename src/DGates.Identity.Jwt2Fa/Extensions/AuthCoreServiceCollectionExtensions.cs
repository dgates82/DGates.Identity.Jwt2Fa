using DGates.Identity.Jwt2Fa.Jwt;
using DGates.Identity.Jwt2Fa.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>
/// DI registration for the core module: JWT issuance/validation, register, login,
/// the password/email lifecycle, and account lookup.
/// </summary>
public static class AuthCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers JWT bearer authentication and the core auth services. Requires zero
    /// extra shape on <typeparamref name="TUser"/> beyond <see cref="IdentityUser"/> —
    /// call <c>AddIdentity&lt;TUser, TRole&gt;()</c> yourself first (this only adds
    /// authentication on top of it, the same way the source app's <c>Program.cs</c> did).
    /// </summary>
    /// <param name="userProjector">
    /// Projects <typeparamref name="TUser"/> down to whatever trimmed, client-safe shape
    /// should be embedded in the JWT and returned from auth responses.
    /// </param>
    public static IServiceCollection AddAuthCore<TUser>(
        this IServiceCollection services,
        IConfiguration configuration,
        Jwt2FaUserProjector<TUser> userProjector)
        where TUser : IdentityUser, new()
    {
        var jwtOptions = configuration.GetSection(JwtOptions.ConfigSection).Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                $"Configuration section '{JwtOptions.ConfigSection}' not found.");

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.ConfigSection));
        services.Configure<AuthCoreOptions>(configuration.GetSection(AuthCoreOptions.ConfigSection));
        services.AddSingleton(userProjector);
        services.AddScoped<IJwtTokenService<TUser>, JwtTokenService<TUser>>();
        services.AddScoped<IAuthCoreService<TUser>, AuthCoreService<TUser>>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.ValidIssuer,
                ValidAudience = jwtOptions.ValidAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecurityKey))
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(Jwt2FaPolicies.AdminOnly, policy => policy.RequireRole(jwtOptions.AdminRoleName));

        return services;
    }
}
