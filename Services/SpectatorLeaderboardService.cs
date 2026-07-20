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
        var season = await GetActiveSeasonAsync();

        if (season == null)
        {
            return new SpectatorRewardSummary
            {
                HasActiveSeason = false,
                RewardPoints = 0,
                BettingPoints = 0,
                BaseOpeningPoints = 0,
                CarriedBonusPoints = 0,
                OpeningTotalPoints = 0,
                WalletStatus = null,
                TotalStakePoints = 0,
                TotalPayoutPoints = 0,
                NetPoints = 0,
                CorrectPredictions = 0,
                TotalPredictions = 0,
                PredictionAccuracy = 0
            };
        }

        var wallet = await _context.SpectatorSeasonWallets
            .AsNoTracking()
            .Where(item => item.SeasonId == season.SeasonId && item.SpectatorId == spectatorId)
            .Select(item => new
            {
                item.SpectatorSeasonWalletId,
                item.OpeningBettingPoints,
                item.CurrentBettingPoints,
                item.SeasonScore,
                item.Status
            })
            .FirstOrDefaultAsync();

        var carriedBonusPoints = wallet == null
            ? 0
            : await _context.PointTransactions
                .AsNoTracking()
                .Where(item =>
                    item.SpectatorSeasonWalletId == wallet.SpectatorSeasonWalletId &&
                    item.TransactionType == PointTransactionTypes.NextSeasonBonus)
                .SumAsync(item => (int?)item.Amount) ?? 0;

        // The season wallet is the source of truth. users.betting_points is only a
        // convenience mirror and must never resurrect a stale balance from an old season.
        var baseOpeningPoints = wallet?.OpeningBettingPoints ?? 0;

        var rows = await _context.RacePredictions
            .AsNoTracking()
            .Where(p =>
                p.SpectatorId == spectatorId &&
                p.Status != RacePredictionStatuses.Cancelled &&
                p.Race.Tournament.SeasonId == season.SeasonId)
            .Select(p => new
            {
                p.PointsAwarded,
                p.StakePoints,
                p.IsCorrect
            })
            .ToListAsync();

        var totalPredictions = rows.Count;
        var correctPredictions = rows.Count(p => p.IsCorrect == true);
        var evaluatedPredictions = rows.Count(p => p.IsCorrect.HasValue);
        var totalStakePoints = rows.Sum(p => p.StakePoints);
        var totalPayoutPoints = rows.Sum(p => p.PointsAwarded);
        var netPoints = totalPayoutPoints - totalStakePoints;

        return new SpectatorRewardSummary
        {
            HasActiveSeason = true,
            RewardPoints = wallet?.SeasonScore ?? 0,
            BettingPoints = wallet?.CurrentBettingPoints ?? 0,
            BaseOpeningPoints = baseOpeningPoints,
            CarriedBonusPoints = carriedBonusPoints,
            OpeningTotalPoints = checked(baseOpeningPoints + carriedBonusPoints),
            WalletStatus = wallet?.Status,
            TotalStakePoints = totalStakePoints,
            TotalPayoutPoints = totalPayoutPoints,
            NetPoints = netPoints,
            CorrectPredictions = correctPredictions,
            TotalPredictions = totalPredictions,
            PredictionAccuracy = evaluatedPredictions == 0
                ? 0
                : Math.Round((decimal)correctPredictions / evaluatedPredictions * 100, 2)
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
                r.Race.Status == RaceStatuses.Published &&
                r.Race.Tournament.Status == TournamentStatuses.Completed &&
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

    public async Task<IReadOnlyList<PredictorLeaderboardItem>> GetPredictorLeaderboardAsync(int limit, int seasonId)
    {
        var rows = await _context.RacePredictions
            .AsNoTracking()
            .Where(p =>
                p.Status == RacePredictionStatuses.Evaluated &&
                p.Race.Status == RaceStatuses.Published &&
                p.Race.Tournament.Status == TournamentStatuses.Completed &&
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
                LegacyScore = g.Sum(p => p.PointsAwarded),
                CorrectPredictions = g.Count(p => p.IsCorrect == true),
                TotalPredictions = g.Count()
            })
            .ToListAsync();

        var spectatorIds = rows.Select(item => item.SpectatorId).ToArray();
        var walletScores = await _context.SpectatorSeasonWallets
            .AsNoTracking()
            .Where(item => item.SeasonId == seasonId && spectatorIds.Contains(item.SpectatorId))
            .ToDictionaryAsync(item => item.SpectatorId, item => item.SeasonScore);

        var ranked = rows
            .Select(r => new PredictorLeaderboardItem
            {
                SpectatorId = r.SpectatorId,
                SpectatorName = r.SpectatorName,
                Points = walletScores.GetValueOrDefault(r.SpectatorId, r.LegacyScore),
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