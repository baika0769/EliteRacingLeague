using Eliteracingleague.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eliteracingleague.API.Controllers.Public;

[AllowAnonymous]
[ApiController]
[Route("api/public")]
public class PublicHomeController : ControllerBase
{
    private readonly PublicHomeService _publicHomeService;

    public PublicHomeController(PublicHomeService publicHomeService)
    {
        _publicHomeService = publicHomeService;
    }

    [HttpGet("home")]
    public async Task<IActionResult> GetHomePage(CancellationToken cancellationToken)
    {
        var response = await _publicHomeService.GetHomePageAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("tournaments")]
    public async Task<IActionResult> GetUpcomingTournaments(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var response = await _publicHomeService.GetUpcomingTournamentsAsync(limit, cancellationToken);
        return Ok(response);
    }
}
