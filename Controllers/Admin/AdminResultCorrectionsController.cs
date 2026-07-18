using Eliteracingleague.API.Constants;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Services.Racing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eliteracingleague.API.Controllers.Admin;

[ApiController]
[Route("api/admin/results/corrections")]
[Authorize(Roles = UserRoles.Admin)]
public class AdminResultCorrectionsController : ControllerBase
{
    private readonly RaceResultCorrectionService _service;

    public AdminResultCorrectionsController(RaceResultCorrectionService service)
    {
        _service = service;
    }

    [HttpPost("race/{raceId:int}/reopen")]
    public async Task<IActionResult> Reopen(
        int raceId,
        ReopenPublishedRaceRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        var result = await _service.ReopenAsync(raceId, adminId, request.Reason, cancellationToken);
        return Ok(new
        {
            message = "Published results were reopened. Payouts were reversed and predictions are locked for re-evaluation.",
            result.RaceId,
            result.ResultsReopened,
            result.PredictionsReset,
            result.PayoutPointsReversed,
            result.RevisionNumber
        });
    }
}
