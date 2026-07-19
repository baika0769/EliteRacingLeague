using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
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
                .Include(t => t.Races)
                    .ThenInclude(r => r.RefereeAssignments)
                .Where(t =>
                    (t.Status == TournamentStatuses.OpenRegistration &&
                     t.StartDate < localToday) ||
                    (RegistrationClosedTournamentStatuses.Contains(t.Status) &&
                     t.Races.Any(r =>
                         r.Status == RaceStatuses.Scheduled &&
                         r.RefereeAssignments.Any(a =>
                             a.Status == RefereeAssignmentStatuses.Assigned))))
                .ToListAsync(cancellationToken);

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

                foreach (var race in tournament.Races)
                {
                    var hasActiveRefereeAssignment = race.RefereeAssignments.Any(a =>
                        a.Status == RefereeAssignmentStatuses.Assigned);

                    if (race.Status == RaceStatuses.Scheduled &&
                        hasActiveRefereeAssignment)
                    {
                        race.Status = RaceStatuses.AssignedReferee;
                        race.UpdatedAt = effectiveUtcNow;
                        hasChanges = true;
                    }
                }
            }

            if (hasChanges)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
