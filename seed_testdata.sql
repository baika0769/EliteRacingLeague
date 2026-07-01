-- ============================================================
-- ELITE RACING LEAGUE — LOCAL TEST DATA SEED
-- Password for ALL accounts: Test@123
-- Run this on your local SQL Server (EliteRacingLeague DB)
-- Safe to re-run: uses IF NOT EXISTS checks
-- ============================================================

USE EliteRacingLeague;
GO

-- ─── 1. USERS ────────────────────────────────────────────────

-- Admin
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'admin@test.com')
INSERT INTO Users (FullName, Email, PasswordHash, Phone, Role, Status, EmailVerified, CreatedAt)
VALUES (
    'Admin Test',
    'admin@test.com',
    'AQAAAAEAACcQAAAAEG4WPf4UcsACZdoPPL9Q7To9jlfYZlXQjFpSxnH3H/7pkW+Nk/VNgWwvuE6ZCTU2XA==',
    '0900000001',
    'Admin',
    'Active',
    1,
    GETDATE()
);

-- Horse Owner 1
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'owner1@test.com')
INSERT INTO Users (FullName, Email, PasswordHash, Phone, Role, Status, EmailVerified, CreatedAt)
VALUES (
    'Nguyen Van Owner',
    'owner1@test.com',
    'AQAAAAEAACcQAAAAEMQqZlca0ihHjTXBkJOxAdKzqrDooRCxnsZglzkj6zgzdGkHOWG7BMmzfp0Mw5GtKQ==',
    '0900000002',
    'HorseOwner',
    'Active',
    1,
    GETDATE()
);

-- Horse Owner 2
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'owner2@test.com')
INSERT INTO Users (FullName, Email, PasswordHash, Phone, Role, Status, EmailVerified, CreatedAt)
VALUES (
    'Tran Thi Owner',
    'owner2@test.com',
    'AQAAAAEAACcQAAAAEBlx05Otk+8EogRZ8EOO0jZ6+ReNP3vVsWaFbOL955pW4YTwFC5fCrEdyc6MOBnBLQ==',
    '0900000003',
    'HorseOwner',
    'Active',
    1,
    GETDATE()
);

-- Jockey 1
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'jockey1@test.com')
INSERT INTO Users (FullName, Email, PasswordHash, Phone, Role, Status, EmailVerified, CreatedAt)
VALUES (
    'Le Van Jockey',
    'jockey1@test.com',
    'AQAAAAEAACcQAAAAEBuIRg0Uz5qaeyXH6GeyaBiFYdOxVy71R9cyWldnIeP3YjlrF5d606Ld64aCH3Z1+A==',
    '0900000004',
    'Jockey',
    'Active',
    1,
    GETDATE()
);

-- Jockey 2
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'jockey2@test.com')
INSERT INTO Users (FullName, Email, PasswordHash, Phone, Role, Status, EmailVerified, CreatedAt)
VALUES (
    'Pham Thi Jockey',
    'jockey2@test.com',
    'AQAAAAEAACcQAAAAEE4zdTcMwBO1TrpVIAt9lv+b58Tswn0I5R54xCS83BblL6rkUHNBbnINVIQXlFjbYw==',
    '0900000005',
    'Jockey',
    'Active',
    1,
    GETDATE()
);

-- Race Referee
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'referee1@test.com')
INSERT INTO Users (FullName, Email, PasswordHash, Phone, Role, Status, EmailVerified, CreatedAt)
VALUES (
    'Hoang Van Referee',
    'referee1@test.com',
    'AQAAAAEAACcQAAAAEAlqCD0471Y9g/ZHmQHpcarlWSgNPGNG6VRIttd7ReYzyFuGTeuk77ZnYSw8cWImDQ==',
    '0900000006',
    'RaceReferee',
    'Active',
    1,
    GETDATE()
);

-- Spectator
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'spectator1@test.com')
INSERT INTO Users (FullName, Email, PasswordHash, Phone, Role, Status, EmailVerified, CreatedAt)
VALUES (
    'Nguyen Thi Spectator',
    'spectator1@test.com',
    'AQAAAAEAACcQAAAAEGvmrXVx3VvreddzQ97TL8ChhTcZG/VJgat2NZqVfIzJjdNWFQuu6Cfbye/YSl7VJg==',
    '0900000007',
    'Spectator',
    'Active',
    1,
    GETDATE()
);

-- ─── 2. HORSE OWNER PROFILES ─────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM HorseOwners WHERE OwnerId = (SELECT UserId FROM Users WHERE Email = 'owner1@test.com'))
INSERT INTO HorseOwners (OwnerId, Address, IsActive, CreatedAt)
SELECT UserId, '123 Test Street, Ho Chi Minh City', 1, GETDATE()
FROM Users WHERE Email = 'owner1@test.com';

IF NOT EXISTS (SELECT 1 FROM HorseOwners WHERE OwnerId = (SELECT UserId FROM Users WHERE Email = 'owner2@test.com'))
INSERT INTO HorseOwners (OwnerId, Address, IsActive, CreatedAt)
SELECT UserId, '456 Demo Avenue, Ha Noi', 1, GETDATE()
FROM Users WHERE Email = 'owner2@test.com';

-- ─── 3. JOCKEY PROFILES ──────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM Jockeys WHERE JockeyId = (SELECT UserId FROM Users WHERE Email = 'jockey1@test.com'))
INSERT INTO Jockeys (JockeyId, WeightKg, YearsOfExperience, HealthStatus, CertificateNo, IsActive, CreatedAt)
SELECT UserId, 58.5, 5, 'Good', 'JKY-2024-001', 1, GETDATE()
FROM Users WHERE Email = 'jockey1@test.com';

IF NOT EXISTS (SELECT 1 FROM Jockeys WHERE JockeyId = (SELECT UserId FROM Users WHERE Email = 'jockey2@test.com'))
INSERT INTO Jockeys (JockeyId, WeightKg, YearsOfExperience, HealthStatus, CertificateNo, IsActive, CreatedAt)
SELECT UserId, 55.0, 3, 'Excellent', 'JKY-2024-002', 1, GETDATE()
FROM Users WHERE Email = 'jockey2@test.com';

-- ─── 4. REFEREE PROFILE ──────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM RaceReferees WHERE RefereeId = (SELECT UserId FROM Users WHERE Email = 'referee1@test.com'))
INSERT INTO RaceReferees (RefereeId, LicenseNo, ExperienceYears, IsActive, CreatedAt)
SELECT UserId, 'REF-2024-001', 7, 1, GETDATE()
FROM Users WHERE Email = 'referee1@test.com';

-- ─── 5. HORSE BREEDS ─────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM HorseBreeds WHERE BreedName = 'Thoroughbred')
INSERT INTO HorseBreeds (BreedName, Description, IsActive)
VALUES ('Thoroughbred', 'A hot-blooded horse breed known for speed and agility.', 1);

IF NOT EXISTS (SELECT 1 FROM HorseBreeds WHERE BreedName = 'Arabian')
INSERT INTO HorseBreeds (BreedName, Description, IsActive)
VALUES ('Arabian', 'One of the oldest horse breeds, known for endurance.', 1);

IF NOT EXISTS (SELECT 1 FROM HorseBreeds WHERE BreedName = 'Quarter Horse')
INSERT INTO HorseBreeds (BreedName, Description, IsActive)
VALUES ('Quarter Horse', 'Excels at sprinting short distances.', 1);

-- ─── 6. HORSES ───────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM Horses WHERE HorseName = 'Thunder Strike')
INSERT INTO Horses (OwnerId, BreedId, HorseName, Age, HeightCm, WeightKg, HealthStatus, AchievementSummary, IsActive, CreatedAt)
SELECT
    (SELECT UserId FROM Users WHERE Email = 'owner1@test.com'),
    (SELECT TOP 1 BreedId FROM HorseBreeds WHERE BreedName = 'Thoroughbred'),
    'Thunder Strike',
    4,
    162.5,
    480.0,
    'Good',
    'Winner of 3 regional races in 2023',
    1,
    GETDATE();

IF NOT EXISTS (SELECT 1 FROM Horses WHERE HorseName = 'Silver Wind')
INSERT INTO Horses (OwnerId, BreedId, HorseName, Age, HeightCm, WeightKg, HealthStatus, AchievementSummary, IsActive, CreatedAt)
SELECT
    (SELECT UserId FROM Users WHERE Email = 'owner1@test.com'),
    (SELECT TOP 1 BreedId FROM HorseBreeds WHERE BreedName = 'Arabian'),
    'Silver Wind',
    6,
    158.0,
    460.0,
    'Excellent',
    'Top 5 finisher in national championship 2022',
    1,
    GETDATE();

IF NOT EXISTS (SELECT 1 FROM Horses WHERE HorseName = 'Golden Flash')
INSERT INTO Horses (OwnerId, BreedId, HorseName, Age, HeightCm, WeightKg, HealthStatus, AchievementSummary, IsActive, CreatedAt)
SELECT
    (SELECT UserId FROM Users WHERE Email = 'owner2@test.com'),
    (SELECT TOP 1 BreedId FROM HorseBreeds WHERE BreedName = 'Quarter Horse'),
    'Golden Flash',
    5,
    155.0,
    450.0,
    'Good',
    'Sprint specialist, 5 wins in 2023',
    1,
    GETDATE();

-- ─── 7. SEASON ───────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM Seasons WHERE SeasonName = 'Season 2025')
INSERT INTO Seasons (SeasonName, StartDate, EndDate, Status, PointsPerCorrectPrediction, CreatedAt)
VALUES ('Season 2025', '2025-01-01', '2025-12-31', 'Active', 10, GETDATE());

-- ─── 8. TOURNAMENT ───────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM Tournaments WHERE TournamentName = 'Spring Championship 2025')
INSERT INTO Tournaments (TournamentName, Description, StartDate, EndDate, Location, MaxHorses, PrizePool, Rules, Status, SeasonId, CreatedBy, CreatedAt)
SELECT
    'Spring Championship 2025',
    'Annual spring horse racing championship open to all registered horses.',
    '2025-07-01',
    '2025-07-31',
    'Ho Chi Minh City Racecourse',
    20,
    50000000.00,
    'All registered horses must pass pre-race health inspection. Riders must hold valid jockey license.',
    'Open',
    (SELECT TOP 1 SeasonId FROM Seasons WHERE SeasonName = 'Season 2025'),
    (SELECT UserId FROM Users WHERE Email = 'admin@test.com'),
    GETDATE();

-- ─── 9. RACE ─────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM Races WHERE RaceName = 'Opening Sprint - 1000m')
INSERT INTO Races (TournamentId, RaceName, RaceDate, DistanceMeters, Location, MaxHorses, JockeySelectionDeadline, PredictionDeadline, Status, CreatedAt)
SELECT
    (SELECT TOP 1 TournamentId FROM Tournaments WHERE TournamentName = 'Spring Championship 2025'),
    'Opening Sprint - 1000m',
    '2025-07-05 09:00:00',
    1000,
    'Ho Chi Minh City Racecourse - Track A',
    10,
    '2025-07-04 18:00:00',
    '2025-07-05 08:00:00',
    'Scheduled',
    GETDATE();

-- ─── SUMMARY ─────────────────────────────────────────────────

PRINT '✅ Seed completed! Test accounts (password: Test@123):';
PRINT '   admin@test.com       → Admin';
PRINT '   owner1@test.com      → HorseOwner (2 horses: Thunder Strike, Silver Wind)';
PRINT '   owner2@test.com      → HorseOwner (1 horse: Golden Flash)';
PRINT '   jockey1@test.com     → Jockey (active, 5 yrs exp)';
PRINT '   jockey2@test.com     → Jockey (active, 3 yrs exp)';
PRINT '   referee1@test.com    → RaceReferee (active)';
PRINT '   spectator1@test.com  → Spectator';
PRINT '';
PRINT '   Season: Season 2025 (Active)';
PRINT '   Tournament: Spring Championship 2025 (Open)';
PRINT '   Race: Opening Sprint - 1000m (Scheduled, 2025-07-05)';
GO
