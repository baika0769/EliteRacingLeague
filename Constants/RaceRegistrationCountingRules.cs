namespace Eliteracingleague.API.Constants;

public static class RaceRegistrationCountingRules
{
    public static readonly string[] ReservedStatuses =
    {
        RaceRegistrationStatuses.Pending,
        RaceRegistrationStatuses.Approved,
        RaceRegistrationStatuses.JockeyInvited,
        RaceRegistrationStatuses.ReadyToRace,
        RaceRegistrationStatuses.Completed
    };

    public static readonly string[] ConfirmedStatuses =
    {
        RaceRegistrationStatuses.Approved,
        RaceRegistrationStatuses.JockeyInvited,
        RaceRegistrationStatuses.ReadyToRace,
        RaceRegistrationStatuses.Completed
    };

    public static readonly string[] ReadyStatuses =
    {
        RaceRegistrationStatuses.ReadyToRace,
        RaceRegistrationStatuses.Completed
    };

    public static readonly string[] VisibleParticipantStatuses =
    {
        RaceRegistrationStatuses.Pending,
        RaceRegistrationStatuses.Approved,
        RaceRegistrationStatuses.JockeyInvited,
        RaceRegistrationStatuses.ReadyToRace,
        RaceRegistrationStatuses.Completed
    };
}
