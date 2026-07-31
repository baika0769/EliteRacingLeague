using Eliteracingleague.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eliteracingleague.API.Migrations;

[DbContext(typeof(EliteRacingLeagueContext))]
[Migration("20260801020000_AllowRepredictionAfterCancellation")]
public partial class AllowRepredictionAfterCancellation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UQ_race_predictions_race_spectator",
            table: "race_predictions");

        migrationBuilder.CreateIndex(
            name: "UQ_race_predictions_race_spectator",
            table: "race_predictions",
            columns: new[] { "race_id", "spectator_id" },
            unique: true,
            filter: "[status] <> 'Cancelled'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UQ_race_predictions_race_spectator",
            table: "race_predictions");

        migrationBuilder.CreateIndex(
            name: "UQ_race_predictions_race_spectator",
            table: "race_predictions",
            columns: new[] { "race_id", "spectator_id" },
            unique: true);
    }
}
