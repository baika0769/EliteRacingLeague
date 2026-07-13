namespace Eliteracingleague.API.Services.SystemTime;

public class RaceTimeStatusBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RaceTimeStatusBackgroundService> _logger;

    public RaceTimeStatusBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RaceTimeStatusBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configuredInterval = _configuration.GetValue(
            "SystemTime:SyncIntervalSeconds",
            60);

        var intervalSeconds = Math.Clamp(configuredInterval, 5, 3600);

        _logger.LogInformation(
            "Race time status synchronization started with interval {IntervalSeconds}s.",
            intervalSeconds);

        // Run once immediately instead of waiting for the first timer tick.
        await SynchronizeOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(intervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SynchronizeOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during application shutdown.
        }
    }

    private async Task SynchronizeOnceAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var service = scope.ServiceProvider
                .GetRequiredService<IRaceTimeStatusService>();

            var result = await service.SyncAsync(cancellationToken);

            if (result.ExpiredInvitations > 0 ||
                result.UpdatedRaces > 0 ||
                result.UpdatedTournaments > 0)
            {
                _logger.LogInformation(
                    "Time status sync completed. Expired invitations: {ExpiredInvitations}; updated races: {UpdatedRaces}; updated tournaments: {UpdatedTournaments}.",
                    result.ExpiredInvitations,
                    result.UpdatedRaces,
                    result.UpdatedTournaments);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Race time status synchronization failed.");
        }
    }
}