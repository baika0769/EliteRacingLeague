using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services
{
    public class TournamentStatusService
    {
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
            var localNow = _dateTimeProvider.GetLocalNow(
                _dateTimeProvider.TimeZoneId);

            var localToday = DateOnly.FromDateTime(localNow);

            var tournaments = await _context.Tournaments
                .Include(t => t.Race)
                .Where(t =>
                    t.Status == TournamentStatuses.OpenRegistration &&
                    t.StartDate < localToday)
                .ToListAsync(cancellationToken);

            if (tournaments.Count == 0)
            {
                return;
            }

            foreach (var tournament in tournaments)
            {
                
                tournament.Status = TournamentStatuses.ClosedRegistration;
                tournament.UpdatedAt = localNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}