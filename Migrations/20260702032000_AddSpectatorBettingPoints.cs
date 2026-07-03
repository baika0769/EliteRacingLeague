using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eliteracingleague.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSpectatorBettingPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.users', 'betting_points') IS NULL
BEGIN
    ALTER TABLE dbo.users
    ADD betting_points INT NOT NULL
        CONSTRAINT DF_users_betting_points DEFAULT(0);
END;

UPDATE dbo.users
SET betting_points = 1000
WHERE role = 'Spectator'
  AND betting_points = 0;

IF COL_LENGTH('dbo.race_predictions', 'stake_points') IS NULL
BEGIN
    ALTER TABLE dbo.race_predictions
    ADD stake_points INT NOT NULL
        CONSTRAINT DF_race_predictions_stake_points DEFAULT(0);
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.race_predictions', 'stake_points') IS NOT NULL
BEGIN
    IF OBJECT_ID('DF_race_predictions_stake_points', 'D') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.race_predictions DROP CONSTRAINT DF_race_predictions_stake_points;
    END;

    ALTER TABLE dbo.race_predictions DROP COLUMN stake_points;
END;

IF COL_LENGTH('dbo.users', 'betting_points') IS NOT NULL
BEGIN
    IF OBJECT_ID('DF_users_betting_points', 'D') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.users DROP CONSTRAINT DF_users_betting_points;
    END;

    ALTER TABLE dbo.users DROP COLUMN betting_points;
END;
");
        }
    }
}
