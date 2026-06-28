using Eliteracingleague.API.DTOs.Admin;

namespace Eliteracingleague.API.Services.SystemTime;

public interface IRaceTimeStatusService
{
    Task<SyncTimeStatusesResponse> SyncAsync(CancellationToken cancellationToken = default);
}
