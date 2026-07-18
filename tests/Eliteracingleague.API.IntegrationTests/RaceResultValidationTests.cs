using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.Racing;
using Xunit;

namespace Eliteracingleague.API.IntegrationTests;

public sealed class RaceResultValidationTests
{
    private readonly RaceResultValidationService _service = new();

    [Fact]
    public void DsqResult_WithFinishPosition_IsRejected()
    {
        var results = new[]
        {
            new RaceResult
            {
                ResultId = 1,
                RegistrationId = 10,
                OutcomeStatus = RaceOutcomeStatuses.Disqualified,
                FinishPosition = 1,
                Status = RaceResultStatuses.RefereeConfirmed
            },
            new RaceResult
            {
                ResultId = 2,
                RegistrationId = 11,
                OutcomeStatus = RaceOutcomeStatuses.Finished,
                FinishPosition = 1,
                FinishTimeSeconds = 90,
                Status = RaceResultStatuses.RefereeConfirmed
            }
        };

        var errors = _service.ValidateForPublication(
            results,
            new HashSet<int> { 10, 11 },
            new HashSet<int> { 10 });

        Assert.Contains(errors, message => message.Contains("must not have a finish position"));
    }

    [Fact]
    public void CompleteValidResults_AreAccepted()
    {
        var results = new[]
        {
            new RaceResult
            {
                ResultId = 1,
                RegistrationId = 10,
                OutcomeStatus = RaceOutcomeStatuses.Finished,
                FinishPosition = 1,
                FinishTimeSeconds = 90,
                Status = RaceResultStatuses.RefereeConfirmed
            },
            new RaceResult
            {
                ResultId = 2,
                RegistrationId = 11,
                OutcomeStatus = RaceOutcomeStatuses.DidNotFinish,
                Status = RaceResultStatuses.RefereeConfirmed
            }
        };

        var errors = _service.ValidateForPublication(
            results,
            new HashSet<int> { 10, 11 },
            new HashSet<int>());

        Assert.Empty(errors);
    }
}
