using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eliteracingleague.API.Migrations
{
    public partial class FixRaceResultPositionIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
IF EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE name = N'UQ_race_results_race_position'
      AND parent_object_id = OBJECT_ID(N'dbo.race_results')
)
BEGIN
    ALTER TABLE dbo.race_results
    DROP CONSTRAINT UQ_race_results_race_position;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UQ_race_results_race_position'
      AND object_id = OBJECT_ID(N'dbo.race_results')
)
BEGIN
    DROP INDEX UQ_race_results_race_position
    ON dbo.race_results;
END;

CREATE UNIQUE INDEX UQ_race_results_race_position
ON dbo.race_results
(
    race_id,
    finish_position
)
WHERE finish_position IS NOT NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UQ_race_results_race_position'
      AND object_id = OBJECT_ID(N'dbo.race_results')
)
BEGIN
    DROP INDEX UQ_race_results_race_position
    ON dbo.race_results;
END;

ALTER TABLE dbo.race_results
ADD CONSTRAINT UQ_race_results_race_position
UNIQUE
(
    race_id,
    finish_position
);
");
        }
    }
}