using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services
{
    public class TournamentStatusService
    {
        private readonly EliteRacingLeagueContext _context;

        public TournamentStatusService(EliteRacingLeagueContext context)
        {
            _context = context;
        }

        public async Task SyncTournamentStatusesAsync()
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);

            var tournaments = await _context.Tournaments
                .Include(t => t.Race)
                .Where(t =>
                    t.Status != TournamentStatuses.Cancelled &&
                    t.Status != TournamentStatuses.Completed)
                .ToListAsync();

            foreach (var tournament in tournaments)
            {
                // Draft là Admin chưa mở đăng ký, auto không được tự đổi
                if (tournament.Status == TournamentStatuses.Draft)
                {
                    continue;
                }

                var race = tournament.Race;

                if (race == null)
                {
                    continue;
                }

                // OpenRegistration + quá deadline => ClosedRegistration
                // StartDate của tournament bạn đang dùng làm RegistrationDeadline
                if (tournament.Status == TournamentStatuses.OpenRegistration &&
                    tournament.StartDate < today)
                {
                    tournament.Status = TournamentStatuses.ClosedRegistration;
                    tournament.UpdatedAt = now;
                }

                // Không auto chuyển race/tournament sang Ongoing.
                // Race start phải do Referee bấm Start sau inspection + Mark Ready.
                // Service này chỉ tự đóng đăng ký theo deadline.
            }

            await _context.SaveChangesAsync();
        }
    }
}