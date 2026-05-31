using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;

namespace Eliteracingleague.API.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    public class AdminUsersController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;

        public AdminUsersController(EliteRacingLeagueContext context)
        {
            _context = context;
        }

        // Lấy danh sách tất cả user
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.Status,
                    u.EmailVerified,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        // Lấy chi tiết 1 user theo ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await _context.Users
                .Where(u => u.UserId == id)
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.Status,
                    u.EmailVerified,
                    u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound(new { message = "User not found" });

            return Ok(user);
        }
    }
}