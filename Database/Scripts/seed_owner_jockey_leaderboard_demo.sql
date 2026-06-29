USE [EliteRacingLeague];
GO
-- If your local database has a different name, change the USE statement above.
-- Script: seed_owner_jockey_leaderboard_demo.sql

SET NOCOUNT ON;

DECLARE @PasswordHash varchar(255) = 'DemoPasswordHash_NotForLogin';

DECLARE @DemoUsers table
(
    FullName nvarchar(150) NOT NULL,
    Email varchar(255) NOT NULL,
    Role varchar(30) NOT NULL
);

INSERT INTO @DemoUsers (FullName, Email, Role)
VALUES
    (N'Historical Demo Owner 1', 'demo.leaderboard.owner1@example.com', 'HorseOwner'),
    (N'Historical Demo Owner 2', 'demo.leaderboard.owner2@example.com', 'HorseOwner'),
    (N'Historical Demo Owner 3', 'demo.leaderboard.owner3@example.com', 'HorseOwner'),
    (N'Historical Demo Owner 4', 'demo.leaderboard.owner4@example.com', 'HorseOwner'),
    (N'Historical Demo Jockey 1', 'demo.leaderboard.jockey1@example.com', 'Jockey'),
    (N'Historical Demo Jockey 2', 'demo.leaderboard.jockey2@example.com', 'Jockey'),
    (N'Historical Demo Jockey 3', 'demo.leaderboard.jockey3@example.com', 'Jockey'),
    (N'Historical Demo Jockey 4', 'demo.leaderboard.jockey4@example.com', 'Jockey'),
    (N'Historical Demo Referee', 'demo.leaderboard.referee@example.com', 'RaceReferee'),
    (N'Historical Demo Admin', 'demo.leaderboard.admin@example.com', 'Admin');

INSERT INTO dbo.users (full_name, email, password_hash, role, status, email_verified, created_at)
SELECT u.FullName, u.Email, @PasswordHash, u.Role, 'Active', 1, SYSUTCDATETIME()
FROM @DemoUsers u
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.users existing
    WHERE existing.email = u.Email
);

DECLARE @Owner1Id int = (SELECT user_id FROM dbo.users WHERE email = 'demo.leaderboard.owner1@example.com');
DECLARE @Owner2Id int = (SELECT user_id FROM dbo.users WHERE email = 'demo.leaderboard.owner2@example.com');
DECLARE @Owner3Id int = (SELECT user_id FROM dbo.users WHERE email = 'demo.leaderboard.owner3@example.com');
DECLARE @Owner4Id int = (SELECT user_id FROM dbo.users WHERE email = 'demo.leaderboard.owner4@example.com');
DECLARE @Jockey1Id int = (SELECT user_id FROM dbo.users WHERE email = 'demo.leaderboard.jockey1@example.com');
DECLARE @Jockey2Id int = (SELECT user_id FROM dbo.users WHERE email = 'demo.leaderboard.jockey2@example.com');
DECLARE @Jockey3Id int = (SELECT user_id FROM dbo.users WHERE email = 'demo.leaderboard.jockey3@example.com');
DECLARE @Jockey4Id int = (SELECT user_id FROM dbo.users WHERE email = 'demo.leaderboard.jockey4@example.com');
DECLARE @RefereeId int = (SELECT user_id FROM dbo.users WHERE email = 'demo.leaderboard.referee@example.com');
DECLARE @AdminId int = (SELECT user_id FROM dbo.users WHERE email = 'demo.leaderboard.admin@example.com');

INSERT INTO dbo.horse_owners (owner_id, address, created_at, is_active)
SELECT owner_id, N'Historical Demo leaderboard address', SYSUTCDATETIME(), 1
FROM (VALUES (@Owner1Id), (@Owner2Id), (@Owner3Id), (@Owner4Id)) owners(owner_id)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.horse_owners existing
WHERE existing.owner_id = owners.owner_id
);

INSERT INTO dbo.jockeys (jockey_id, weight_kg, years_of_experience, health_status, certificate_no, is_active, created_at)
SELECT jockey_id, weight_kg, years_of_experience, 'Fit', certificate_no, 1, SYSUTCDATETIME()
FROM
(
    VALUES
        (@Jockey1Id, CAST(51.5 AS decimal(5, 2)), 5, 'DEMO-LB-J1'),
        (@Jockey2Id, CAST(52.0 AS decimal(5, 2)), 4, 'DEMO-LB-J2'),
        (@Jockey3Id, CAST(50.5 AS decimal(5, 2)), 6, 'DEMO-LB-J3'),
        (@Jockey4Id, CAST(53.0 AS decimal(5, 2)), 3, 'DEMO-LB-J4')
) jockeys(jockey_id, weight_kg, years_of_experience, certificate_no)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.jockeys existing
    WHERE existing.jockey_id = jockeys.jockey_id
);

INSERT INTO dbo.race_referees (referee_id, license_no, experience_years, is_active, created_at)
SELECT @RefereeId, 'DEMO-LB-REF', 8, 1, SYSUTCDATETIME()
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.race_referees existing
    WHERE existing.referee_id = @RefereeId
);

IF NOT EXISTS (SELECT 1 FROM dbo.horse_breeds WHERE breed_name = N'Demo Leaderboard Thoroughbred')
BEGIN
    INSERT INTO dbo.horse_breeds (breed_name, description, is_active)
    VALUES (N'Demo Leaderboard Thoroughbred', N'Historical Demo breed for leaderboard testing', 1);
END;

DECLARE @BreedId int = (SELECT breed_id FROM dbo.horse_breeds WHERE breed_name = N'Demo Leaderboard Thoroughbred');

INSERT INTO dbo.horses (owner_id, breed_id, horse_name, age, height_cm, weight_kg, health_status, achievement_summary, is_active, created_at)
SELECT owner_id, @BreedId, horse_name, 5, CAST(160.00 AS decimal(5, 2)), weight_kg, 'Healthy', N'Historical Demo leaderboard horse', 1, SYSUTCDATETIME()
FROM
(
    VALUES
        (@Owner1Id, N'Historical Demo Horse 1', CAST(470.00 AS decimal(6, 2))),
        (@Owner2Id, N'Historical Demo Horse 2', CAST(465.00 AS decimal(6, 2))),
        (@Owner3Id, N'Historical Demo Horse 3', CAST(472.00 AS decimal(6, 2))),
        (@Owner4Id, N'Historical Demo Horse 4', CAST(468.00 AS decimal(6, 2)))
) horses(owner_id, horse_name, weight_kg)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.horses existing
    WHERE existing.horse_name = horses.horse_name
);

DECLARE @Horse1Id int = (SELECT horse_id FROM dbo.horses WHERE horse_name = N'Historical Demo Horse 1');
DECLARE @Horse2Id int = (SELECT horse_id FROM dbo.horses WHERE horse_name = N'Historical Demo Horse 2');
DECLARE @Horse3Id int = (SELECT horse_id FROM dbo.horses WHERE horse_name = N'Historical Demo Horse 3');
DECLARE @Horse4Id int = (SELECT horse_id FROM dbo.horses WHERE horse_name = N'Historical Demo Horse 4');

IF NOT EXISTS (SELECT 1 FROM dbo.seasons WHERE season_name = N'Demo Leaderboard Historical Season')
BEGIN
    INSERT INTO dbo.seasons (season_name, start_date, end_date, status, points_per_correct_prediction, created_at)
    VALUES (N'Demo Leaderboard Historical Season', '2024-01-01', '2026-12-31', 'Closed', 100, SYSUTCDATETIME());
END;
DECLARE @SeasonId int = (SELECT season_id FROM dbo.seasons WHERE season_name = N'Demo Leaderboard Historical Season');

DECLARE @Events table
(
    EventNo int NOT NULL PRIMARY KEY,
    TournamentName nvarchar(200) NOT NULL,
    RaceName nvarchar(200) NOT NULL,
    StartDate date NOT NULL,
    EndDate date NOT NULL,
    RaceDate datetime2 NOT NULL,
    Location nvarchar(255) NOT NULL,
    PrizePool decimal(18, 2) NOT NULL
);

INSERT INTO @Events (EventNo, TournamentName, RaceName, StartDate, EndDate, RaceDate, Location, PrizePool)
VALUES
    (1, N'Historical Demo Leaderboard Cup 2024', N'Historical Demo Race 2024', '2024-03-01', '2024-03-03', '2024-03-03T09:00:00', N'Demo Track A', 1900.00),
    (2, N'Historical Demo Leaderboard Cup 2025', N'Historical Demo Race 2025', '2025-05-01', '2025-05-03', '2025-05-03T09:00:00', N'Demo Track B', 1900.00),
    (3, N'Historical Demo Leaderboard Cup 2026', N'Historical Demo Race 2026', '2026-01-10', '2026-01-12', '2026-01-12T09:00:00', N'Demo Track C', 1900.00);

INSERT INTO dbo.tournaments (tournament_name, description, start_date, end_date, location, max_horses, prize_pool, rules, status, season_id, created_by, created_at)
SELECT e.TournamentName, N'Historical Demo tournament for Owner/Jockey leaderboard testing', e.StartDate, e.EndDate, e.Location, 4, e.PrizePool, N'Demo Leaderboard rules', 'Completed', @SeasonId, @AdminId, SYSUTCDATETIME()
FROM @Events e
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.tournaments existing
    WHERE existing.tournament_name = e.TournamentName
);

INSERT INTO dbo.races (tournament_id, race_name, race_date, distance_meters, location, max_horses, status, created_at)
SELECT t.tournament_id, e.RaceName, e.RaceDate, 1000, e.Location, 4, 'Published', SYSUTCDATETIME()
FROM @Events e
JOIN dbo.tournaments t ON t.tournament_name = e.TournamentName
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.races existing
    WHERE existing.tournament_id = t.tournament_id
);

DECLARE @Participants table
(
    ParticipantNo int NOT NULL PRIMARY KEY,
    OwnerId int NOT NULL,
    HorseId int NOT NULL,
    JockeyId int NOT NULL
);

INSERT INTO @Participants (ParticipantNo, OwnerId, HorseId, JockeyId)
VALUES
    (1, @Owner1Id, @Horse1Id, @Jockey1Id),
    (2, @Owner2Id, @Horse2Id, @Jockey2Id),
    (3, @Owner3Id, @Horse3Id, @Jockey3Id),
    (4, @Owner4Id, @Horse4Id, @Jockey4Id);

INSERT INTO dbo.race_registrations (race_id, horse_id, owner_id, jockey_id, status, submitted_at, reviewed_by, reviewed_at, jockey_confirmed_at, admin_note)
SELECT r.race_id, p.HorseId, p.OwnerId, p.JockeyId, 'Completed', DATEADD(day, -20, r.race_date), @AdminId, DATEADD(day, -19, r.race_date), DATEADD(day, -18, r.race_date), N'Historical Demo leaderboard registration'
FROM @Events e
JOIN dbo.races r ON r.race_name = e.RaceName
CROSS JOIN @Participants p
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.race_registrations existing
    WHERE existing.race_id = r.race_id
      AND existing.horse_id = p.HorseId
);

DECLARE @Finishes table
(
EventNo int NOT NULL,
    ParticipantNo int NOT NULL,
    FinishPosition int NOT NULL,
    FinishTimeSeconds decimal(10, 3) NOT NULL,
    Score decimal(10, 2) NOT NULL
);

INSERT INTO @Finishes (EventNo, ParticipantNo, FinishPosition, FinishTimeSeconds, Score)
VALUES
    (1, 1, 1, 95.120, 100.00),
    (1, 2, 2, 96.450, 85.00),
    (1, 3, 3, 97.300, 70.00),
    (1, 4, 4, 99.800, 55.00),
    (2, 2, 1, 94.880, 100.00),
    (2, 1, 2, 95.950, 85.00),
    (2, 3, 3, 96.700, 70.00),
    (2, 4, 4, 98.900, 55.00),
    (3, 1, 1, 94.500, 100.00),
    (3, 3, 2, 95.600, 85.00),
    (3, 2, 3, 96.250, 70.00),
    (3, 4, 4, 98.100, 55.00);

INSERT INTO dbo.race_results (race_id, registration_id, finish_time_seconds, finish_position, score, status, entered_by_referee_id, admin_confirmed_by, published_at, note, created_at)
SELECT r.race_id, rr.registration_id, f.FinishTimeSeconds, f.FinishPosition, f.Score,
       CASE WHEN e.EventNo = 2 THEN 'AdminApproved' ELSE 'Published' END,
       @RefereeId, @AdminId, DATEADD(hour, 2, r.race_date), N'Historical Demo leaderboard result', SYSUTCDATETIME()
FROM @Events e
JOIN dbo.races r ON r.race_name = e.RaceName
JOIN @Finishes f ON f.EventNo = e.EventNo
JOIN @Participants p ON p.ParticipantNo = f.ParticipantNo
JOIN dbo.race_registrations rr ON rr.race_id = r.race_id AND rr.horse_id = p.HorseId
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.race_results existing
    WHERE existing.registration_id = rr.registration_id
);

INSERT INTO dbo.prize_rules (race_id, rank_position, prize_amount, note)
SELECT r.race_id, prizes.RankPosition, prizes.PrizeAmount, N'Demo Leaderboard prize rule'
FROM @Events e
JOIN dbo.races r ON r.race_name = e.RaceName
CROSS JOIN
(
    VALUES
        (1, CAST(1000.00 AS decimal(18, 2))),
        (2, CAST(600.00 AS decimal(18, 2))),
        (3, CAST(300.00 AS decimal(18, 2)))
) prizes(RankPosition, PrizeAmount)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.prize_rules existing
    WHERE existing.race_id = r.race_id
      AND existing.rank_position = prizes.RankPosition
);

INSERT INTO dbo.prize_awards (race_id, registration_id, owner_id, jockey_id, rank_position, prize_amount, status, paid_at, created_at)
SELECT result.race_id, result.registration_id, registration.owner_id, registration.jockey_id, result.finish_position, ruleRow.prize_amount,
       CASE WHEN result.finish_position = 1 THEN 'Paid' ELSE 'Pending' END,
       CASE WHEN result.finish_position = 1 THEN DATEADD(hour, 4, result.published_at) ELSE NULL END,
       SYSUTCDATETIME()
FROM dbo.race_results result
JOIN dbo.race_registrations registration ON registration.registration_id = result.registration_id
JOIN dbo.races race ON race.race_id = result.race_id
JOIN @Events e ON e.RaceName = race.race_name
JOIN dbo.prize_rules ruleRow ON ruleRow.race_id = result.race_id AND ruleRow.rank_position = result.finish_position
WHERE result.finish_position <= 3
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.prize_awards existing
WHERE existing.race_id = result.race_id
        AND existing.rank_position = result.finish_position
  );

SELECT
    @SeasonId AS DemoSeasonId,
    COUNT(DISTINCT r.race_id) AS DemoRaceCount,
    COUNT(DISTINCT rr.registration_id) AS DemoRegistrationCount,
    COUNT(DISTINCT result.result_id) AS DemoResultCount
FROM @Events e
JOIN dbo.races r ON r.race_name = e.RaceName
LEFT JOIN dbo.race_registrations rr ON rr.race_id = r.race_id
LEFT JOIN dbo.race_results result ON result.race_id = r.race_id;