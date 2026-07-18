using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Models;

namespace Eliteracingleague.API.Services.Racing;

public class RaceResultValidationService
{
    public IReadOnlyList<string> ValidateForPublication(
        IReadOnlyCollection<RaceResult> results,
        IReadOnlySet<int> eligibleRegistrationIds,
        IReadOnlySet<int> disqualifiedRegistrationIds)
    {
        var errors = new List<string>();

        if (results.Count == 0)
        {
            errors.Add("The race has no results.");
            return errors;
        }

        var duplicates = results.GroupBy(r => r.RegistrationId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
            errors.Add($"Duplicate result rows for registrations: {string.Join(", ", duplicates)}.");

        var missing = eligibleRegistrationIds.Except(results.Select(r => r.RegistrationId)).ToList();
        if (missing.Count > 0)
            errors.Add($"Missing results for registrations: {string.Join(", ", missing)}.");

        foreach (var result in results)
        {
            if (!RaceOutcomeStatuses.IsValid(result.OutcomeStatus))
            {
                errors.Add($"Result #{result.ResultId} has invalid outcome '{result.OutcomeStatus}'.");
                continue;
            }

            if (disqualifiedRegistrationIds.Contains(result.RegistrationId) &&
                result.OutcomeStatus != RaceOutcomeStatuses.Disqualified)
            {
                errors.Add($"Registration #{result.RegistrationId} has a disqualifying violation and must use outcome DSQ.");
            }

            if (result.OutcomeStatus == RaceOutcomeStatuses.Finished)
            {
                if (!result.FinishPosition.HasValue || result.FinishPosition <= 0)
                    errors.Add($"Finished result #{result.ResultId} requires a positive finish position.");
                if (!result.FinishTimeSeconds.HasValue || result.FinishTimeSeconds <= 0)
                    errors.Add($"Finished result #{result.ResultId} requires a positive finish time.");
            }
            else if (result.FinishPosition.HasValue)
            {
                errors.Add($"Outcome {result.OutcomeStatus} for result #{result.ResultId} must not have a finish position.");
            }

            if (result.Status is not RaceResultStatuses.RefereeConfirmed and not RaceResultStatuses.AdminApproved)
                errors.Add($"Result #{result.ResultId} must be referee-confirmed before publication.");
        }

        var rankable = results.Where(r => r.OutcomeStatus == RaceOutcomeStatuses.Finished).ToList();
        var duplicatePositions = rankable.Where(r => r.FinishPosition.HasValue)
            .GroupBy(r => r.FinishPosition!.Value)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicatePositions.Count > 0)
            errors.Add($"Duplicate finish positions: {string.Join(", ", duplicatePositions)}.");

        if (rankable.Count == 0)
            errors.Add("At least one participant must finish the race.");
        else if (rankable.Count(r => r.FinishPosition == 1) != 1)
            errors.Add("Exactly one finished participant must hold position 1.");

        return errors;
    }

    public static string NormalizeOutcome(string? outcome) =>
        RaceOutcomeStatuses.IsValid(outcome) ? outcome! : RaceOutcomeStatuses.Finished;
}
