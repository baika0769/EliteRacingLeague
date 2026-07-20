using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Services.Racing;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services
{
    public class TournamentStatusService
    {
        private static readonly string[] RegistrationClosedTournamentStatuses =
        {
            TournamentStatuses.ClosedRegistration,
            TournamentStatuses.Ongoing
        };

        private readonly EliteRacingLeagueContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;

        public TournamentStatusService(
            EliteRacingLeagueContext context,
            IDateTimeProvider dateTimeProvider)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task SyncTournamentStatusesAsync(
            CancellationToken cancellationToken = default)
        {
            var effectiveUtcNow = _dateTimeProvider.UtcNow;
            var localNow = _dateTimeProvider.GetLocalNow(
                _dateTimeProvider.TimeZoneId);
            var localToday = DateOnly.FromDateTime(localNow);

            var tournaments = await _context.Tournaments
                .Include(tournament => tournament.Races)
                    .ThenInclude(race => race.RefereeAssignments)
                .Where(tournament =>
                    (tournament.Status == TournamentStatuses.OpenRegistration &&
                     tournament.StartDate < localToday) ||
                    (RegistrationClosedTournamentStatuses.Contains(tournament.Status) &&
                     tournament.Races.Any(race =>
                         race.Status == RaceStatuses.Scheduled &&
                         race.RefereeAssignments.Any(assignment =>
                             assignment.Status == RefereeAssignmentStatuses.Assigned))))
                .ToListAsync(cancellationToken);

            var alreadyClosedTournamentIds = await _context.Tournaments
                .AsNoTracking()
                .Where(tournament =>
                    RegistrationClosedTournamentStatuses.Contains(tournament.Status))
                .Select(tournament => tournament.TournamentId)
                .ToListAsync(cancellationToken);

            var closedTournamentIds = new HashSet<int>(alreadyClosedTournamentIds);
            var hasChanges = false;

            foreach (var tournament in tournaments)
            {
                if (tournament.Status == TournamentStatuses.OpenRegistration &&
                    tournament.StartDate < localToday)
                {
                    tournament.Status = TournamentStatuses.ClosedRegistration;
                    tournament.UpdatedAt = effectiveUtcNow;
                    hasChanges = true;
                }

                if (!RegistrationClosedTournamentStatuses.Contains(tournament.Status))
                {
                    continue;
                }

                closedTournamentIds.Add(tournament.TournamentId);

                foreach (var race in tournament.Races)
                {
                    var hasActiveRefereeAssignment = race.RefereeAssignments.Any(assignment =>
                        assignment.Status == RefereeAssignmentStatuses.Assigned);

                    if (race.Status == RaceStatuses.Scheduled &&
                        hasActiveRefereeAssignment)
                    {
                        race.Status = RaceStatuses.AssignedReferee;
                        race.UpdatedAt = effectiveUtcNow;
                        hasChanges = true;
                    }
                }
            }

            var closureResult = await RegistrationClosureHelper.ApplyAsync(
                _context,
                closedTournamentIds,
                effectiveUtcNow,
                cancellationToken);

            if (hasChanges || closureResult.HasChanges)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
