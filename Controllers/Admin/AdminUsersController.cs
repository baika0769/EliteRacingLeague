using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;

namespace Eliteracingleague.API.Controllers.Admin
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

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .Select(u => new AdminUserResponse
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Role = u.Role,
                    Status = u.Status,
                    EmailVerified = u.EmailVerified,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await _context.Users
                .Where(u => u.UserId == id)
                .Select(u => new AdminUserResponse
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Role = u.Role,
                    Status = u.Status,
                    EmailVerified = u.EmailVerified,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound(new AdminActionResponse
                {
                    Message = "User not found",
                    Id = id
                });

            return Ok(user);
        }

        [HttpPut("{id}/block")]
        public async Task<IActionResult> BlockUser(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
                return NotFound(new AdminActionResponse
                {
                    Message = "User not found",
                    Id = id
                });

            user.Status = "Suspended";
            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "User blocked successfully",
                Id = user.UserId,
                Name = user.FullName,
                Status = user.Status
            });
        }

        [HttpPut("{id}/unblock")]
        public async Task<IActionResult> UnblockUser(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
                return NotFound(new AdminActionResponse
                {
                    Message = "User not found",
                    Id = id
                });

            user.Status = "Active";
            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "User unblocked successfully",
                Id = user.UserId,
                Name = user.FullName,
                Status = user.Status
            });
        }
    }
}