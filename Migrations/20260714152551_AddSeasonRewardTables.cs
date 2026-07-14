using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eliteracingleague.API.Migrations
{
    public partial class AddSeasonRewardTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "season_reward_rules",
                columns: table => new
                {
                    season_reward_rule_id = table.Column<int>(
                        type: "int",
                        nullable: false)
                        .Annotation(
                            "SqlServer:Identity",
                            "1, 1"),

                    season_id = table.Column<int>(
                        type: "int",
                        nullable: false),

                    rank_position = table.Column<int>(
                        type: "int",
                        nullable: false),

                    reward_name = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false),

                    reward_description = table.Column<string>(
                        type: "nvarchar(max)",
                        nullable: true),

                    bonus_points = table.Column<int>(
                        type: "int",
                        nullable: false),

                    created_at = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: false,
                        defaultValueSql: "(sysutcdatetime())"),

                    updated_at = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_season_reward_rules",
                        x => x.season_reward_rule_id);

                    table.ForeignKey(
                        name: "FK_season_reward_rules_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "season_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "season_rewards",
                columns: table => new
                {
                    season_reward_id = table.Column<int>(
                        type: "int",
                        nullable: false)
                        .Annotation(
                            "SqlServer:Identity",
                            "1, 1"),

                    season_id = table.Column<int>(
                        type: "int",
                        nullable: false),

                    spectator_id = table.Column<int>(
                        type: "int",
                        nullable: false),

                    rank_position = table.Column<int>(
                        type: "int",
                        nullable: false),

                    final_points = table.Column<int>(
                        type: "int",
                        nullable: false),

                    reward_name = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false),

                    reward_description = table.Column<string>(
                        type: "nvarchar(max)",
                        nullable: true),

                    bonus_points = table.Column<int>(
                        type: "int",
                        nullable: false),

                    is_bonus_applied = table.Column<bool>(
                        type: "bit",
                        nullable: false),

                    applied_to_season_id = table.Column<int>(
                        type: "int",
                        nullable: true),

                    applied_at = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: true),

                    status = table.Column<string>(
                        type: "varchar(30)",
                        unicode: false,
                        maxLength: 30,
                        nullable: false),

                    awarded_at = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_season_rewards",
                        x => x.season_reward_id);

                    table.ForeignKey(
                        name: "FK_season_rewards_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "season_id",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "FK_season_rewards_users_spectator_id",
                        column: x => x.spectator_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_season_reward_rules_season_id_rank_position",
                table: "season_reward_rules",
                columns: new[]
                {
                    "season_id",
                    "rank_position"
                },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_season_rewards_season_id_spectator_id",
                table: "season_rewards",
                columns: new[]
                {
                    "season_id",
                    "spectator_id"
                },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_season_rewards_spectator_id",
                table: "season_rewards",
                column: "spectator_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "season_reward_rules");

            migrationBuilder.DropTable(
                name: "season_rewards");
        }
    }
}