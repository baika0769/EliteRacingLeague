using Eliteracingleague.API.DTOs.Leaderboards;

namespace Eliteracingleague.API.Services.Leaderboards;

public interface ILeaderboardService
{
    Task<IReadOnlyList<OwnerLeaderboardItemResponse>> GetOwnerLeaderboardAsync(
        int? seasonId,
        int? year,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JockeyLeaderboardItemResponse>> GetJockeyLeaderboardAsync(
        int? seasonId,
        int? year,
        int limit,
        CancellationToken cancellationToken = default);
}
