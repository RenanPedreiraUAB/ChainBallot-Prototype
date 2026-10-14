using ElectionAuthorization.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ElectionAuthorization.Api.Tests;

/// <summary>
/// Test host factory for Election Authorization API integration tests.
/// </summary>
public sealed class AuthorizationApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AuthorizationDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            var dbPath = Path.Combine(Path.GetTempPath(), $"election-authz-tests-{Guid.NewGuid():N}.db");
            services.AddDbContext<AuthorizationDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        });
    }
}
