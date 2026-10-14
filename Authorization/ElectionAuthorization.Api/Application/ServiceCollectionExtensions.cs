using System.Text;
using ElectionAuthorization.Api.Data;
using ElectionAuthorization.Api.Repositories;
using ElectionAuthorization.Api.Security;
using ElectionAuthorization.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ElectionAuthorization.Api.Application;

/// <summary>
/// Registers Election Authorization services and infrastructure dependencies.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Election Authorization component dependencies.
    /// </summary>
    /// <param name="services">Dependency injection service collection.</param>
    /// <param name="configuration">Application configuration source.</param>
    /// <returns>Updated service collection.</returns>
    /// <exception cref="InvalidOperationException">Thrown when trusted JWT signing key is missing or weak.</exception>
    public static IServiceCollection AddElectionAuthorizationCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthorizationAdminOptions>(configuration.GetSection(AuthorizationAdminOptions.SectionName));
        services.Configure<AuthorizationJwtOptions>(configuration.GetSection(AuthorizationJwtOptions.SectionName));

        var jwtOptions = configuration.GetSection(AuthorizationJwtOptions.SectionName).Get<AuthorizationJwtOptions>()
            ?? new AuthorizationJwtOptions();

        var envSigningKey = Environment.GetEnvironmentVariable("CHAINBALLOT_AUTHZ_TRUSTEDAUTH_SIGNING_KEY");
        if (!string.IsNullOrWhiteSpace(envSigningKey))
        {
            jwtOptions.SigningKey = envSigningKey;
        }

        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("TrustedAuth:SigningKey must be configured with at least 32 characters.");
        }

        var connectionString = configuration.GetConnectionString("AuthorizationDb")
            ?? "Data Source=election-authorization.db";

        services.AddDbContext<AuthorizationDbContext>(options => options.UseSqlite(connectionString));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    NameClaimType = "unique_name"
                };
            });

        services.AddAuthorization();
        services.AddScoped<IElectionAuthorizationService, ElectionAuthorizationService>();
        services.AddScoped<IVoterIdentityRepository, VoterIdentityRepository>();
        services.AddScoped<IElectionAuthorizationRepository, ElectionAuthorizationRepository>();
        services.AddScoped<IAuthorizationGrantRepository, AuthorizationGrantRepository>();
        services.AddScoped<IAuthorizationAuditRepository, AuthorizationAuditRepository>();
        services.AddSingleton<IVidGenerator, CryptographicVidGenerator>();

        return services;
    }
}
