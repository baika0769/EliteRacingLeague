using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services.Racing;

public class RaceSchedulingValidationService
{
    private readonly EliteRacingLeagueContext _context;

    public RaceSchedulingValidationService(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    public async Task ValidateRaceDatesAsync(
        Tournament tournament,
        DateTime raceDate,
        DateTime? jockeyDeadline,
        DateTime? predictionDeadline,
        int? excludingRaceId = null,
        CancellationToken cancellationToken = default)
    {
        var tournamentStart = tournament.StartDate.ToDateTime(TimeOnly.MinValue);
        var tournamentEnd = tournament.EndDate.ToDateTime(TimeOnly.MaxValue);

        if (raceDate < tournamentStart || raceDate > tournamentEnd)
        {
            throw new InvalidOperationException("Race date must be inside the tournament date range.");
        }

        if (jockeyDeadline.HasValue && jockeyDeadline.Value >= raceDate)
        {
            throw new InvalidOperationException("Jockey selection deadline must be before race date.");
        }

        if (predictionDeadline.HasValue && predictionDeadline.Value >= raceDate)
        {
            throw new InvalidOperationException("Prediction deadline must be before race date.");
        }

        if (predictionDeadline.HasValue && jockeyDeadline.HasValue &&
            predictionDeadline.Value < jockeyDeadline.Value)
        {
            // Allowed but explicit: predictions may open before jockey is selected. No error.
        }

        var duplicateTime = await _context.Races.AsNoTracking().AnyAsync(r =>
            r.TournamentId == tournament.TournamentId &&
            (!excludingRaceId.HasValue || r.RaceId != excludingRaceId.Value) &&
            r.Status != RaceStatuses.Cancelled &&
            r.RaceDate == raceDate,
            cancellationToken);

        if (duplicateTime)
        {
            throw new InvalidOperationException("Another race in this tournament already uses the same start time.");
        }
    }

    public async Task EnsureHorseAndJockeyNoConflictAsync(
        int raceId,
        int horseId,
        int? jockeyId,
        CancellationToken cancellationToken = default)
    {
        var race = await _context.Races.AsNoTracking()
            .Where(r => r.RaceId == raceId)
            .Select(r => new { r.RaceId, r.RaceDate, r.Status })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Race not found.");

        var lower = race.RaceDate.AddHours(-2);
        var upper = race.RaceDate.AddHours(2);

        var horseConflict = await _context.RaceRegistrations.AsNoTracking().AnyAsync(r =>
            r.RaceId != raceId &&
            r.HorseId == horseId &&
            r.Status != RaceRegistrationStatuses.Rejected &&
            r.Status != RaceRegistrationStatuses.Cancelled &&
            r.Status != RaceRegistrationStatuses.Withdrawn &&
            r.Race.Status != RaceStatuses.Cancelled &&
            r.Race.RaceDate > lower && r.Race.RaceDate < upper,
            cancellationToken);

        if (horseConflict)
        {
            throw new InvalidOperationException("The horse is already registered for another race in the same time window.");
        }

        if (!jockeyId.HasValue) return;

        var jockeyConflict = await _context.RaceRegistrations.AsNoTracking().AnyAsync(r =>
            r.RaceId != raceId &&
            r.JockeyId == jockeyId.Value &&
            r.Status != RaceRegistrationStatuses.Rejected &&
            r.Status != RaceRegistrationStatuses.Cancelled &&
            r.Status != RaceRegistrationStatuses.Withdrawn &&
            r.Race.Status != RaceStatuses.Cancelled &&
            r.Race.RaceDate > lower && r.Race.RaceDate < upper,
            cancellationToken);

        if (jockeyConflict)
        {
            throw new InvalidOperationException("The jockey is already assigned to another race in the same time window.");
        }
    }
}
