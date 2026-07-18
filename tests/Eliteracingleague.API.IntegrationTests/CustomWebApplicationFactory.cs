using Eliteracingleague.API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Eliteracingleague.API.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "integration-test-key-that-is-at-least-32-bytes-long",
                ["Jwt:Issuer"] = "EliteRacingLeague.Tests",
                ["Jwt:Audience"] = "EliteRacingLeague.Tests",
                ["Frontend:BaseUrl"] = "http://localhost"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<EliteRacingLeagueContext>>();
            services.RemoveAll<EliteRacingLeagueContext>();
            services.AddDbContext<EliteRacingLeagueContext>(options =>
                options.UseInMemoryDatabase($"EliteRacingLeagueTests-{Guid.NewGuid()}"));
        });
    }
}
