using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Spectator;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services;

public class SpectatorLeaderboardService
{
    private readonly EliteRacingLeagueContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    private static readonly string[] CountedResultStatuses =
    {
        RaceResultStatuses.AdminApproved,
        RaceResultStatuses.Published
    };

    public SpectatorLeaderboardService(
        EliteRacingLeagueContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Season?> GetActiveSeasonAsync()
    {
        return await _context.Seasons
            .AsNoTracking()
            .Where(s => s.Status == SeasonStatuses.Active)
            .OrderByDescending(s => s.StartDate)
            .ThenByDescending(s => s.SeasonId)
            .FirstOrDefaultAsync();
    }

    public async Task<CurrentSeasonResponse?> GetCurrentSeasonResponseAsync()
    {
        var season = await GetActiveSeasonAsync();

        if (season == null)
        {
            return null;
        }

        var today = _dateTimeProvider.UtcNow.Date;
        var startDate = season.StartDate.Date;
        var endDate = season.EndDate.Date;

        var seasonPredictions = _context.RacePredictions
            .AsNoTracking()
            .Where(p =>
                p.Status != RacePredictionStatuses.Cancelled &&
                p.Race.Tournament.SeasonId == season.SeasonId);

        var totalPredictions = await seasonPredictions.CountAsync();

        var totalPredictors = await seasonPredictions
            .Select(p => p.SpectatorId)
            .Distinct()
            .CountAsync();

        return new CurrentSeasonResponse
        {
            SeasonId = season.SeasonId,
            SeasonName = season.SeasonName,
            StartDate = season.StartDate,
            EndDate = season.EndDate,
            DaysLeft = Math.Max(0, (endDate - today).Days),
            TotalDays = Math.Max(0, (endDate - startDate).Days + 1),
            TotalPredictors = totalPredictors,
            TotalPredictions = totalPredictions
        };
    }

    public async Task<int> GetMyRankAsync(int spectatorId)
    {
        var season = await GetActiveSeasonAsync();

        if (season == null)
        {
            return 0;
        }

        var leaderboard = await GetPredictorLeaderboardAsync(int.MaxValue, season.SeasonId);

        return leaderboard.FirstOrDefault(p => p.SpectatorId == spectatorId)?.Rank ?? 0;
    }

    public async Task<int> GetActiveSeasonTotalDaysAsync()
    {
        var season = await GetActiveSeasonAsync();

        if (season == null)
        {
            return 0;
        }

        return Math.Max(0, (season.EndDate.Date - season.StartDate.Date).Days + 1);
    }

    public async Task<SpectatorRewardSummary> GetRewardSummaryAsync(int spectatorId)
    {
        var rows = await _context.RacePredictions
            .AsNoTracking()
            .Where(p =>
                p.SpectatorId == spectatorId &&
                p.Status != RacePredictionStatuses.Cancelled)
            .Select(p => new
            {
                p.PointsAwarded,
                p.StakePoints,
                p.IsCorrect
            })
            .ToListAsync();

        var totalPredictions = rows.Count;
        var correctPredictions = rows.Count(p => p.IsCorrect == true);

        var userBalance = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == spectatorId)
            .Select(u => (int?)u.BettingPoints)
            .FirstOrDefaultAsync() ?? 0;

        var totalStakePoints = rows.Sum(p => p.StakePoints);
        var totalPayoutPoints = rows.Sum(p => p.PointsAwarded);
        var netPoints = totalPayoutPoints - totalStakePoints;

        return new SpectatorRewardSummary
        {
            RewardPoints = netPoints,
            BettingPoints = userBalance,
            TotalStakePoints = totalStakePoints,
            TotalPayoutPoints = totalPayoutPoints,
            NetPoints = netPoints,
            CorrectPredictions = correctPredictions,
            TotalPredictions = totalPredictions,
            PredictionAccuracy = totalPredictions == 0
                ? 0
                : Math.Round((decimal)correctPredictions / totalPredictions * 100, 2)
        };
    }

    public async Task<IReadOnlyList<PredictorLeaderboardItem>> GetPredictorLeaderboardAsync(int limit = 50)
    {
        var season = await GetActiveSeasonAsync();

        if (season == null)
        {
            return Array.Empty<PredictorLeaderboardItem>();
        }

        return await GetPredictorLeaderboardAsync(limit, season.SeasonId);
    }

    public async Task<IReadOnlyList<HorseLeaderboardItem>> GetHorseLeaderboardAsync(int limit = 50)
    {
        var season = await GetActiveSeasonAsync();

        if (season == null)
        {
            return Array.Empty<HorseLeaderboardItem>();
        }

        var rows = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.Race.Tournament.SeasonId == season.SeasonId &&
                r.RaceResult != null &&
                CountedResultStatuses.Contains(r.RaceResult.Status))
            .GroupBy(r => new
            {
                r.HorseId,
                r.Horse.HorseName,
                r.Horse.ImageUrl,
                BreedName = r.Horse.Breed.BreedName,
                OwnerName = r.Horse.Owner.Owner.FullName
            })
            .Select(g => new
            {
                g.Key.HorseId,
                g.Key.HorseName,
                g.Key.ImageUrl,
                g.Key.BreedName,
                g.Key.OwnerName,
                Wins = g.Count(r => r.RaceResult != null && r.RaceResult.FinishPosition == 1),
                TotalRaces = g.Count()
            })
            .ToListAsync();

        var ranked = rows
            .Select(r => new HorseLeaderboardItem
            {
                HorseId = r.HorseId,
                HorseName = r.HorseName,
                ImageUrl = r.ImageUrl,
                BreedName = r.BreedName,
                OwnerName = r.OwnerName,
                Wins = r.Wins,
                TotalRaces = r.TotalRaces,
                WinRate = r.TotalRaces == 0
                    ? 0
                    : Math.Round((decimal)r.Wins / r.TotalRaces * 100, 2)
            })
            .OrderByDescending(r => r.Wins)
            .ThenByDescending(r => r.WinRate)
            .ThenByDescending(r => r.TotalRaces)
            .ThenBy(r => r.HorseId)
            .Take(limit)
            .ToList();

        for (var i = 0; i < ranked.Count; i++)
        {
            ranked[i].Rank = i + 1;
        }

        return ranked;
    }

    private async Task<IReadOnlyList<PredictorLeaderboardItem>> GetPredictorLeaderboardAsync(int limit, int seasonId)
    {
        var rows = await _context.RacePredictions
            .AsNoTracking()
            .Where(p =>
                p.Status != RacePredictionStatuses.Cancelled &&
                p.Race.Tournament.SeasonId == seasonId)
            .GroupBy(p => new
            {
                p.SpectatorId,
                SpectatorName = p.Spectator.FullName
            })
            .Select(g => new
            {
                g.Key.SpectatorId,
                g.Key.SpectatorName,
                Points = g.Sum(p => p.PointsAwarded - p.StakePoints),
                CorrectPredictions = g.Count(p => p.IsCorrect == true),
                TotalPredictions = g.Count()
            })
            .ToListAsync();

        var ranked = rows
            .Select(r => new PredictorLeaderboardItem
            {
                SpectatorId = r.SpectatorId,
                SpectatorName = r.SpectatorName,
                Points = r.Points,
                CorrectPredictions = r.CorrectPredictions,
                TotalPredictions = r.TotalPredictions,
                Accuracy = r.TotalPredictions == 0
                    ? 0
                    : Math.Round((decimal)r.CorrectPredictions / r.TotalPredictions * 100, 2)
            })
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.CorrectPredictions)
            .ThenByDescending(r => r.Accuracy)
            .ThenByDescending(r => r.TotalPredictions)
            .ThenBy(r => r.SpectatorId)
            .Take(limit)
            .ToList();

        for (var i = 0; i < ranked.Count; i++)
        {
            ranked[i].Rank = i + 1;
        }

        return ranked;
    }
}