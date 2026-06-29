using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Leaderboards;
using Eliteracingleague.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services.Leaderboards;

public class LeaderboardService : ILeaderboardService
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    private static readonly string[] CountedResultStatuses =
    {
        RaceResultStatuses.AdminApproved,
        RaceResultStatuses.Published
    };

    private static readonly string[] CountedPrizeAwardStatuses =
    {
        PrizeAwardStatuses.ReadyToClaim,
        PrizeAwardStatuses.Paid
    };

    private readonly EliteRacingLeagueContext _context;

    public LeaderboardService(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<OwnerLeaderboardItemResponse>> GetOwnerLeaderboardAsync(
        int? seasonId,
        int? year,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit = NormalizeLimit(limit);

        var rows = await BuildCountedRegistrationsQuery(seasonId, year)
            .GroupBy(r => new
            {
                r.OwnerId,
                OwnerName = r.Owner.Owner.FullName
            })
            .Select(g => new
            {
                g.Key.OwnerId,
                g.Key.OwnerName,
                TotalRaces = g.Count(),
                Wins = g.Count(r => r.RaceResult != null && r.RaceResult.FinishPosition == 1),
                Top3Finishes = g.Count(r => r.RaceResult != null && r.RaceResult.FinishPosition <= 3),
                AverageFinishPosition = g.Average(r => r.RaceResult == null ? null : (decimal?)r.RaceResult.FinishPosition),
                BestFinishTimeSeconds = g.Min(r => r.RaceResult == null ? null : r.RaceResult.FinishTimeSeconds)
            })
            .ToListAsync(cancellationToken);

        var prizes = await BuildCountedPrizeAwardsQuery(seasonId, year)
            .GroupBy(p => p.OwnerId)
            .Select(g => new
            {
                OwnerId = g.Key,
                TotalPrize = g.Sum(p => p.PrizeAmount)
            })
            .ToDictionaryAsync(p => p.OwnerId, p => p.TotalPrize, cancellationToken);

        var ranked = rows
            .Select(r =>
            {
                var winRate = r.TotalRaces == 0 ? 0 : Math.Round((decimal)r.Wins / r.TotalRaces * 100, 2);

                return new OwnerLeaderboardItemResponse
                {
                    OwnerId = r.OwnerId,
                    OwnerName = r.OwnerName,
                    TotalRaces = r.TotalRaces,
                    Wins = r.Wins,
                    Top3Finishes = r.Top3Finishes,
                    WinRate = winRate,
                    TotalPrize = prizes.GetValueOrDefault(r.OwnerId),
                    AverageFinishPosition = r.AverageFinishPosition == null
                        ? null
                        : Math.Round(r.AverageFinishPosition.Value, 2),
                    BestFinishTimeSeconds = r.BestFinishTimeSeconds
                };
            })
            .OrderByDescending(r => r.Wins)
            .ThenByDescending(r => r.WinRate)
            .ThenByDescending(r => r.Top3Finishes)
            .ThenByDescending(r => r.TotalPrize)
            .ThenBy(r => r.AverageFinishPosition ?? decimal.MaxValue)
            .ThenByDescending(r => r.TotalRaces)
            .ThenBy(r => r.OwnerId)
            .Take(normalizedLimit)
            .ToList();

        SetOwnerRanks(ranked);
        return ranked;
    }

    public async Task<IReadOnlyList<JockeyLeaderboardItemResponse>> GetJockeyLeaderboardAsync(
        int? seasonId,
        int? year,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit = NormalizeLimit(limit);

        var rows = await BuildCountedRegistrationsQuery(seasonId, year)
            .Where(r => r.JockeyId != null)
            .GroupBy(r => new
            {
                JockeyId = r.JockeyId!.Value,
                JockeyName = r.Jockey!.JockeyNavigation.FullName
            })
            .Select(g => new
            {
                g.Key.JockeyId,
                g.Key.JockeyName,
                TotalRaces = g.Count(),
                Wins = g.Count(r => r.RaceResult != null && r.RaceResult.FinishPosition == 1),
                Top3Finishes = g.Count(r => r.RaceResult != null && r.RaceResult.FinishPosition <= 3),
                AverageFinishPosition = g.Average(r => r.RaceResult == null ? null : (decimal?)r.RaceResult.FinishPosition),
                BestFinishTimeSeconds = g.Min(r => r.RaceResult == null ? null : r.RaceResult.FinishTimeSeconds)
            })
            .ToListAsync(cancellationToken);

        var prizes = await BuildCountedPrizeAwardsQuery(seasonId, year)
            .Where(p => p.JockeyId != null)
            .GroupBy(p => p.JockeyId!.Value)
            .Select(g => new
            {
                JockeyId = g.Key,
                TotalPrize = g.Sum(p => p.PrizeAmount)
            })
            .ToDictionaryAsync(p => p.JockeyId, p => p.TotalPrize, cancellationToken);

        var ranked = rows
            .Select(r =>
            {
                var winRate = r.TotalRaces == 0 ? 0 : Math.Round((decimal)r.Wins / r.TotalRaces * 100, 2);

                return new JockeyLeaderboardItemResponse
                {
                    JockeyId = r.JockeyId,
                    JockeyName = r.JockeyName,
                    TotalRaces = r.TotalRaces,
                    Wins = r.Wins,
                    Top3Finishes = r.Top3Finishes,
                    WinRate = winRate,
                    TotalPrize = prizes.GetValueOrDefault(r.JockeyId),
                    AverageFinishPosition = r.AverageFinishPosition == null
                        ? null
                        : Math.Round(r.AverageFinishPosition.Value, 2),
                    BestFinishTimeSeconds = r.BestFinishTimeSeconds
                };
            })
            .OrderByDescending(r => r.Wins)
            .ThenByDescending(r => r.WinRate)
            .ThenByDescending(r => r.Top3Finishes)
            .ThenByDescending(r => r.TotalPrize)
            .ThenBy(r => r.AverageFinishPosition ?? decimal.MaxValue)
            .ThenByDescending(r => r.TotalRaces)
            .ThenBy(r => r.JockeyId)
            .Take(normalizedLimit)
            .ToList();

        SetJockeyRanks(ranked);
        return ranked;
    }

    private IQueryable<RaceRegistration> BuildCountedRegistrationsQuery(int? seasonId, int? year)
    {
        var query = _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RaceResult != null &&
                CountedResultStatuses.Contains(r.RaceResult.Status));

        if (seasonId.HasValue)
        {
            query = query.Where(r => r.Race.Tournament.SeasonId == seasonId.Value);
        }

        if (year.HasValue)
        {
            query = query.Where(r => r.Race.RaceDate.Year == year.Value);
        }

        return query;
    }

    private IQueryable<PrizeAward> BuildCountedPrizeAwardsQuery(int? seasonId, int? year)
    {
        var query = _context.PrizeAwards
            .AsNoTracking()
            .Where(p =>
                CountedPrizeAwardStatuses.Contains(p.Status) &&
                p.Registration.RaceResult != null &&
                CountedResultStatuses.Contains(p.Registration.RaceResult.Status));

        if (seasonId.HasValue)
        {
            query = query.Where(p => p.Race.Tournament.SeasonId == seasonId.Value);
        }

        if (year.HasValue)
        {
            query = query.Where(p => p.Race.RaceDate.Year == year.Value);
        }

        return query;
    }

    private static int NormalizeLimit(int limit)
    {
        if (limit <= 0)
        {
            return DefaultLimit;
        }

        return Math.Min(limit, MaxLimit);
    }

    private static void SetOwnerRanks(IList<OwnerLeaderboardItemResponse> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            items[i].Rank = i + 1;
        }
    }

    private static void SetJockeyRanks(IList<JockeyLeaderboardItemResponse> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            items[i].Rank = i + 1;
        }
    }
}
