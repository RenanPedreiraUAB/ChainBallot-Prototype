using System.Text;
using CynetAuth.Api.Data;
using CynetAuth.Api.Repositories;
using CynetAuth.Api.Security;
using CynetAuth.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CynetAuth.Api.Application;

/// <summary>
/// Registers CynetAuth application services and infrastructure dependencies.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds CynetAuth core services, persistence, and JWT authentication.
    /// </summary>
    /// <param name="services">Dependency injection service collection.</param>
    /// <param name="configuration">Application configuration source.</param>
    /// <returns>Updated service collection.</returns>
    /// <exception cref="InvalidOperationException">Thrown when JWT signing key is missing or weak.</exception>
    public static IServiceCollection AddCynetAuthCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var envSigningKey = Environment.GetEnvironmentVariable("CHAINBALLOT_CYNETAUTH_JWT_SIGNING_KEY");
        if (!string.IsNullOrWhiteSpace(envSigningKey))
        {
            jwtOptions.SigningKey = envSigningKey;
            services.PostConfigure<JwtOptions>(options => options.SigningKey = envSigningKey);
        }

        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 characters.");
        }

        var connectionString = configuration.GetConnectionString("AuthDb")
            ?? "Data Source=cynetauth.db";

        services.AddDbContext<AuthDbContext>(options => options.UseSqlite(connectionString));

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
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserCredentialRepository, UserCredentialRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        return services;
    }
}
