using Eliteracingleague.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eliteracingleague.API.Migrations;

[DbContext(typeof(EliteRacingLeagueContext))]
[Migration("20260713190000_HardenSeasonLifecycle")]
public partial class HardenSeasonLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM dbo.seasons WHERE end_date < start_date)
    THROW 51001, 'A season has end_date before start_date.', 1;

IF EXISTS (SELECT 1 FROM dbo.seasons WHERE points_per_correct_prediction <= 0)
    THROW 51002, 'A season has non-positive points_per_correct_prediction.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.seasons
    WHERE status NOT IN ('Draft', 'Active', 'Closed', 'Cancelled')
)
    THROW 51003, 'A season has an invalid status.', 1;

IF (SELECT COUNT(*) FROM dbo.seasons WHERE status = 'Active') > 1
    THROW 51004, 'More than one season is Active. Keep only one Active season, then rerun the migration.', 1;

IF OBJECT_ID('dbo.CK_seasons_date_range', 'C') IS NULL
BEGIN
    ALTER TABLE dbo.seasons WITH CHECK
    ADD CONSTRAINT CK_seasons_date_range
        CHECK (end_date >= start_date);
END;

IF OBJECT_ID('dbo.CK_seasons_points_positive', 'C') IS NULL
BEGIN
    ALTER TABLE dbo.seasons WITH CHECK
    ADD CONSTRAINT CK_seasons_points_positive
        CHECK (points_per_correct_prediction > 0);
END;

IF OBJECT_ID('dbo.CK_seasons_status', 'C') IS NULL
BEGIN
    ALTER TABLE dbo.seasons WITH CHECK
    ADD CONSTRAINT CK_seasons_status
        CHECK (status IN ('Draft', 'Active', 'Closed', 'Cancelled'));
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.seasons')
      AND name = 'IX_seasons_date_range'
)
BEGIN
    CREATE INDEX IX_seasons_date_range
        ON dbo.seasons(start_date, end_date);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.seasons')
      AND name = 'UX_seasons_single_active'
)
BEGIN
    CREATE UNIQUE INDEX UX_seasons_single_active
        ON dbo.seasons(status)
        WHERE status = 'Active';
END;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.seasons')
      AND name = 'UX_seasons_single_active'
)
    DROP INDEX UX_seasons_single_active ON dbo.seasons;

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.seasons')
      AND name = 'IX_seasons_date_range'
)
    DROP INDEX IX_seasons_date_range ON dbo.seasons;

IF OBJECT_ID('dbo.CK_seasons_status', 'C') IS NOT NULL
    ALTER TABLE dbo.seasons DROP CONSTRAINT CK_seasons_status;

IF OBJECT_ID('dbo.CK_seasons_points_positive', 'C') IS NOT NULL
    ALTER TABLE dbo.seasons DROP CONSTRAINT CK_seasons_points_positive;

IF OBJECT_ID('dbo.CK_seasons_date_range', 'C') IS NOT NULL
    ALTER TABLE dbo.seasons DROP CONSTRAINT CK_seasons_date_range;
");
    }
}