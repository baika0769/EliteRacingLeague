using Eliteracingleague.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eliteracingleague.API.Migrations;

[DbContext(typeof(EliteRacingLeagueContext))]
[Migration("20260731100000_AllowNegativeSpectatorSeasonScore")]
public partial class AllowNegativeSpectatorSeasonScore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_spectator_season_wallets_score",
            table: "spectator_season_wallets");

        migrationBuilder.AddCheckConstraint(
            name: "CK_spectator_season_wallets_score",
            table: "spectator_season_wallets",
            sql: "[pending_recovery_points] >= 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_spectator_season_wallets_score",
            table: "spectator_season_wallets");

        migrationBuilder.AddCheckConstraint(
            name: "CK_spectator_season_wallets_score",
            table: "spectator_season_wallets",
            sql: "[season_score] >= 0 AND [pending_recovery_points] >= 0");
    }
}
