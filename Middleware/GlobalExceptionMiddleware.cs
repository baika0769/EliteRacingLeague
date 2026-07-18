using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict. CorrelationId={CorrelationId}", context.TraceIdentifier);
            await WriteErrorAsync(context, StatusCodes.Status409Conflict,
                "CONCURRENCY_CONFLICT",
                "The data was changed by another request. Reload and try again.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database update failed. CorrelationId={CorrelationId}", context.TraceIdentifier);
            await WriteErrorAsync(context, StatusCodes.Status409Conflict,
                "DATABASE_CONFLICT",
                "The operation conflicts with current database data.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business rule rejected. CorrelationId={CorrelationId}", context.TraceIdentifier);
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest,
                "BUSINESS_RULE_VIOLATION", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error. CorrelationId={CorrelationId}", context.TraceIdentifier);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError,
                "INTERNAL_SERVER_ERROR",
                "An unexpected error occurred. Use the correlationId when reporting this issue.");
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string code,
        string message)
    {
        if (context.Response.HasStarted) return;
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = $"https://httpstatuses.com/{statusCode}",
            title = message,
            status = statusCode,
            code,
            correlationId = context.TraceIdentifier
        });
    }
}
