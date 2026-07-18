using System.Data;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services.Racing;

public class TournamentStandingService
{
    private static readonly IReadOnlyDictionary<int, int> RankPoints =
        new Dictionary<int, int>
        {
            [1] = 10,
            [2] = 7,
            [3] = 5,
            [4] = 3,
            [5] = 2,
            [6] = 1
        };

    private readonly EliteRacingLeagueContext _context;

    public TournamentStandingService(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TournamentStanding>> RecalculateAsync(
        int tournamentId,
        bool finalize,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var tournament = await _context.Tournaments
            .Include(t => t.Races)
            .FirstOrDefaultAsync(t => t.TournamentId == tournamentId, cancellationToken)
            ?? throw new InvalidOperationException("Tournament not found.");

        var publishedRaceIds = tournament.Races
            .Where(r => r.Status == RaceStatuses.Published)
            .Select(r => r.RaceId)
            .ToList();

        if (finalize)
        {
            if (tournament.Races.Count == 0)
                throw new InvalidOperationException("Tournament has no races.");

            var unfinished = tournament.Races.Any(r =>
                r.Status != RaceStatuses.Published && r.Status != RaceStatuses.Cancelled);

            if (unfinished)
                throw new InvalidOperationException("All races must be Published or Cancelled before finalizing tournament standings.");

            if (publishedRaceIds.Count == 0)
                throw new InvalidOperationException("At least one race must be published before finalizing standings.");
        }

        var results = await _context.RaceResults.AsNoTracking()
            .Where(r => publishedRaceIds.Contains(r.RaceId) &&
                        r.Status == RaceResultStatuses.Published)
            .Select(r => new
            {
                r.RegistrationId,
                r.Registration.HorseId,
                r.Registration.OwnerId,
                r.Registration.JockeyId,
                r.FinishPosition,
                r.FinishTimeSeconds,
                r.OutcomeStatus
            })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var rows = results
            .GroupBy(r => new { r.HorseId, r.OwnerId })
            .Select(group =>
            {
                var rankable = group.Where(x =>
                    x.OutcomeStatus == RaceOutcomeStatuses.Finished &&
                    x.FinishPosition.HasValue).ToList();

                return new TournamentStanding
                {
                    TournamentId = tournamentId,
                    HorseId = group.Key.HorseId,
                    OwnerId = group.Key.OwnerId,
                    // A horse may use different jockeys in different races. Keep the jockey only when it is consistent.
                    JockeyId = group.Select(x => x.JockeyId).Distinct().Count() == 1
                        ? group.Select(x => x.JockeyId).First()
                        : null,
                    TotalPoints = rankable.Sum(x => RankPoints.GetValueOrDefault(x.FinishPosition!.Value, 0)),
                    Wins = rankable.Count(x => x.FinishPosition == 1),
                    SecondPlaces = rankable.Count(x => x.FinishPosition == 2),
                    ThirdPlaces = rankable.Count(x => x.FinishPosition == 3),
                    CompletedRaces = rankable.Count,
                    TotalFinishTimeSeconds = rankable.Sum(x => x.FinishTimeSeconds ?? 0),
                    IsFinal = finalize,
                    CalculatedAt = now,
                    FinalizedAt = finalize ? now : null
                };
            })
            .OrderByDescending(x => x.TotalPoints)
            .ThenByDescending(x => x.Wins)
            .ThenByDescending(x => x.SecondPlaces)
            .ThenByDescending(x => x.ThirdPlaces)
            .ThenBy(x => x.TotalFinishTimeSeconds)
            .ThenBy(x => x.HorseId)
            .ToList();

        for (var i = 0; i < rows.Count; i++)
            rows[i].FinalRank = i + 1;

        var existing = await _context.TournamentStandings
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        if (existing.Any(x => x.IsFinal) && !finalize)
            throw new InvalidOperationException("Final tournament standings cannot be recalculated as provisional.");

        _context.TournamentStandings.RemoveRange(existing);
        _context.TournamentStandings.AddRange(rows);

        if (finalize)
        {
            tournament.Status = TournamentStatuses.Completed;
            tournament.UpdatedAt = now;
        }
        else if (tournament.Status == TournamentStatuses.Completed)
        {
            tournament.Status = TournamentStatuses.Ongoing;
            tournament.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows;
    }
}
