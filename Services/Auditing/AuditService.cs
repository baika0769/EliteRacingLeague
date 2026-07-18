using System.Text.Json;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;

namespace Eliteracingleague.API.Services.Auditing;

public class AuditService : IAuditService
{
    private readonly EliteRacingLeagueContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(
        EliteRacingLeagueContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task WriteAsync(
        int? userId,
        string action,
        string entityType,
        string? entityId,
        object? oldValues = null,
        object? newValues = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var http = _httpContextAccessor.HttpContext;
        var correlationId = http?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValuesJson = oldValues == null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues == null ? null : JsonSerializer.Serialize(newValues),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = http?.Request.Headers.UserAgent.ToString(),
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }
}
