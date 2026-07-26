using Eliteracingleague.API.Constants;

namespace Eliteracingleague.API.Services.Public;

public sealed class RacePublicStateService
{
    public RacePublicState Build(
        string seasonStatus,
        string tournamentStatus,
        string raceStatus,
        DateTime raceDate,
        DateTime? predictionDeadline,
        DateOnly registrationDeadline,
        int maxHorses,
        int reservedCount,
        int confirmedCount,
        int readyCount,
        bool replayAvailable,
        DateTime localNow)
    {
        var normalizedMaxHorses = Math.Max(0, maxHorses);
        var normalizedReserved = Math.Max(0, reservedCount);
        var normalizedConfirmed = Math.Max(0, confirmedCount);
        var normalizedReady = Math.Max(0, readyCount);
        var availableSlots = Math.Max(0, normalizedMaxHorses - normalizedReserved);

        return new RacePublicState
        {
            MaxHorses = normalizedMaxHorses,
            ReservedCount = normalizedReserved,
            ConfirmedCount = normalizedConfirmed,
            ReadyCount = normalizedReady,
            AvailableSlots = availableSlots,
            RegistrationState = GetRegistrationState(
                seasonStatus,
                tournamentStatus,
                raceStatus,
                raceDate,
                registrationDeadline,
                availableSlots,
                localNow),
            PredictionState = GetPredictionState(
                seasonStatus,
                tournamentStatus,
                raceStatus,
                raceDate,
                predictionDeadline,
                localNow),
            ReplayAvailable = replayAvailable
        };
    }

    private static string GetRegistrationState(
        string seasonStatus,
        string tournamentStatus,
        string raceStatus,
        DateTime raceDate,
        DateOnly registrationDeadline,
        int availableSlots,
        DateTime localNow)
    {
        if (seasonStatus != SeasonStatuses.Active)
        {
            return PublicRegistrationStates.SeasonInactive;
        }

        if (tournamentStatus == TournamentStatuses.Cancelled ||
            raceStatus == RaceStatuses.Cancelled)
        {
            return PublicRegistrationStates.Cancelled;
        }

        if (tournamentStatus == TournamentStatuses.Draft)
        {
            return PublicRegistrationStates.Unavailable;
        }

        if (localNow >= raceDate)
        {
            return PublicRegistrationStates.RaceStarted;
        }

        if (!RaceStatuses.CanRegister(raceStatus))
        {
            return PublicRegistrationStates.RaceClosed;
        }

        if (tournamentStatus != TournamentStatuses.OpenRegistration)
        {
            return PublicRegistrationStates.TournamentClosed;
        }

        var deadlineLocal = registrationDeadline.ToDateTime(TimeOnly.MaxValue);
        if (localNow > deadlineLocal)
        {
            return PublicRegistrationStates.DeadlinePassed;
        }

        if (availableSlots <= 0)
        {
            return PublicRegistrationStates.Full;
        }

        return PublicRegistrationStates.Open;
    }

    private static string GetPredictionState(
        string seasonStatus,
        string tournamentStatus,
        string raceStatus,
        DateTime raceDate,
        DateTime? predictionDeadline,
        DateTime localNow)
    {
        if (seasonStatus != SeasonStatuses.Active)
        {
            return PublicPredictionStates.SeasonInactive;
        }

        if (tournamentStatus == TournamentStatuses.Cancelled ||
            raceStatus == RaceStatuses.Cancelled)
        {
            return PublicPredictionStates.Cancelled;
        }

        if (tournamentStatus is not TournamentStatuses.OpenRegistration
            and not TournamentStatuses.ClosedRegistration)
        {
            return tournamentStatus == TournamentStatuses.Draft
                ? PublicPredictionStates.Unavailable
                : PublicPredictionStates.TournamentClosed;
        }

        if (RaceStatuses.IsClosedForPrediction(raceStatus))
        {
            return PublicPredictionStates.RaceClosed;
        }

        if (!predictionDeadline.HasValue)
        {
            return PublicPredictionStates.NotConfigured;
        }

        if (localNow >= raceDate)
        {
            return PublicPredictionStates.RaceStarted;
        }

        if (localNow >= predictionDeadline.Value)
        {
            return PublicPredictionStates.DeadlinePassed;
        }

        return PublicPredictionStates.Open;
    }
}

public sealed class RacePublicState
{
    public int MaxHorses { get; init; }
    public int ReservedCount { get; init; }
    public int ConfirmedCount { get; init; }
    public int ReadyCount { get; init; }
    public int AvailableSlots { get; init; }
    public string RegistrationState { get; init; } = string.Empty;
    public string PredictionState { get; init; } = string.Empty;
    public bool ReplayAvailable { get; init; }
}
