namespace Eliteracingleague.API.Constants;

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string HorseOwner = "HorseOwner";
    public const string Jockey = "Jockey";
    public const string RaceReferee = "RaceReferee";
    public const string Spectator = "Spectator";

    public static readonly string[] All =
    {
        Admin,
        HorseOwner,
        Jockey,
        RaceReferee,
        Spectator
    };

    // Không cho người dùng tự đăng ký Admin hoặc RaceReferee.
    public static readonly string[] RegisterableRoles =
    {
        HorseOwner,
        Jockey,
        Spectator
    };

    public static bool IsValid(string? role)
    {
        return !string.IsNullOrWhiteSpace(role)
            && All.Contains(role);
    }

    public static bool IsRegisterable(string? role)
    {
        return !string.IsNullOrWhiteSpace(role)
            && RegisterableRoles.Contains(role);
    }
}
