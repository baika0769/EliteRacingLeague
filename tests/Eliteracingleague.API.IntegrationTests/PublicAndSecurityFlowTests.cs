using Xunit;
using System.Net;

namespace Eliteracingleague.API.IntegrationTests;

public sealed class PublicAndSecurityFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PublicAndSecurityFlowTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task PublicTournamentDetails_MissingId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/public/tournaments/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminDashboard_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/admin/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuditLogs_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/admin/audit-logs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
