using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Jockey;

[Route("api/jockey/lookups")]
[ApiController]
[Authorize(Roles = UserRoles.Jockey)]
public class JockeyLookupsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public JockeyLookupsController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    [HttpGet("settings-options")]
    public IActionResult GetSettingsOptions()
    {
        return Ok(new
        {
            healthStatuses = HorseHealthStatuses.All,
            distanceOptions = JockeyDistanceMeters.All.Select(distanceMeters => new
            {
                distanceMeters,
                label = JockeyDistanceMeters.Labels[distanceMeters]
            }),
            distanceSkillLevels = JockeyDistanceSkillLevels.All,
            breedExperienceLevels = JockeyBreedSkillLevels.All
        });
    }

    [HttpGet("horse-breeds")]
    public async Task<IActionResult> GetHorseBreeds()
    {
        var breeds = await _context.HorseBreeds
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.BreedName)
            .Select(b => new
            {
                breedId = b.BreedId,
                breedName = b.BreedName
            })
            .ToListAsync();

        return Ok(breeds);
    }
}
