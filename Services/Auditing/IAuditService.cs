namespace Eliteracingleague.API.Services.Auditing;

public interface IAuditService
{
    Task WriteAsync(
        int? userId,
        string action,
        string entityType,
        string? entityId,
        object? oldValues = null,
        object? newValues = null,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
