using CynetAuth.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CynetAuth.Api.Tests;

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AuthDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            var dbPath = Path.Combine(Path.GetTempPath(), $"cynetauth-tests-{Guid.NewGuid():N}.db");
            services.AddDbContext<AuthDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        });
    }
}
