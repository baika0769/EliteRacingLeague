using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[Route("api/owner")]
[ApiController]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerLookupsController : OwnerBaseController
{
    public OwnerLookupsController(EliteRacingLeagueContext context) : base(context)
    {
    }

    [HttpGet("horse-breeds")]
    public async Task<IActionResult> GetHorseBreeds()
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
        }

        var ownerError = await ValidateOwnerCanManageHorsesAsync(ownerId.Value);

        if (ownerError != null)
        {
            return ownerError;
        }

        var breeds = await _context.HorseBreeds
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.BreedName)
            .Select(b => new HorseBreedOptionResponse
            {
                BreedId = b.BreedId,
                BreedName = b.BreedName
            })
            .ToListAsync();

        return Ok(breeds);
    }
}