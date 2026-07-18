using Eliteracingleague.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eliteracingleague.API.Migrations;

[DbContext(typeof(EliteRacingLeagueContext))]
[Migration("20260718223000_AddSeasonWalletLedgerAndRewardClaim")]
public partial class AddSeasonWalletLedgerAndRewardClaim : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_seasons_status",
            table: "seasons");

        migrationBuilder.AddCheckConstraint(
            name: "CK_seasons_status",
            table: "seasons",
            sql: "[status] IN ('Draft', 'Active', 'Settling', 'Closed', 'Cancelled')");

        migrationBuilder.AddColumn<string>(
            name: "admin_note",
            table: "season_rewards",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "approved_at",
            table: "season_rewards",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "claim_deadline",
            table: "season_rewards",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "claimed_at",
            table: "season_rewards",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "delivered_at",
            table: "season_rewards",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "delivery_address",
            table: "season_rewards",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "preparing_at",
            table: "season_rewards",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "receiver_name",
            table: "season_rewards",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "receiver_phone",
            table: "season_rewards",
            type: "varchar(30)",
            unicode: false,
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "rejected_at",
            table: "season_rewards",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "spectator_season_wallets",
            columns: table => new
            {
                spectator_season_wallet_id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                season_id = table.Column<int>(type: "int", nullable: false),
                spectator_id = table.Column<int>(type: "int", nullable: false),
                opening_betting_points = table.Column<int>(type: "int", nullable: false),
                current_betting_points = table.Column<int>(type: "int", nullable: false),
                season_score = table.Column<int>(type: "int", nullable: false),
                final_betting_points = table.Column<int>(type: "int", nullable: true),
                final_season_score = table.Column<int>(type: "int", nullable: true),
                final_rank = table.Column<int>(type: "int", nullable: true),
                status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                opened_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                frozen_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                settled_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_spectator_season_wallets", x => x.spectator_season_wallet_id);
                table.CheckConstraint("CK_spectator_season_wallets_balance", "[opening_betting_points] >= 0 AND [current_betting_points] >= 0");
                table.CheckConstraint("CK_spectator_season_wallets_score", "[season_score] >= 0");
                table.CheckConstraint("CK_spectator_season_wallets_status", "[status] IN ('Active', 'Frozen', 'Settled')");
                table.ForeignKey(
                    name: "FK_spectator_season_wallets_seasons_season_id",
                    column: x => x.season_id,
                    principalTable: "seasons",
                    principalColumn: "season_id");
                table.ForeignKey(
                    name: "FK_spectator_season_wallets_users_spectator_id",
                    column: x => x.spectator_id,
                    principalTable: "users",
                    principalColumn: "user_id");
            });

        migrationBuilder.CreateTable(
            name: "point_transactions",
            columns: table => new
            {
                point_transaction_id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                spectator_season_wallet_id = table.Column<int>(type: "int", nullable: false),
                transaction_type = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                amount = table.Column<int>(type: "int", nullable: false),
                score_delta = table.Column<int>(type: "int", nullable: false),
                balance_before = table.Column<int>(type: "int", nullable: false),
                balance_after = table.Column<int>(type: "int", nullable: false),
                reference_type = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                reference_id = table.Column<int>(type: "int", nullable: true),
                idempotency_key = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_point_transactions", x => x.point_transaction_id);
                table.CheckConstraint("CK_point_transactions_balance", "[balance_before] >= 0 AND [balance_after] >= 0");
                table.ForeignKey(
                    name: "FK_point_transactions_spectator_season_wallets_spectator_season_wallet_id",
                    column: x => x.spectator_season_wallet_id,
                    principalTable: "spectator_season_wallets",
                    principalColumn: "spectator_season_wallet_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_spectator_season_wallets_season_score",
            table: "spectator_season_wallets",
            columns: new[] { "season_id", "season_score" });

        migrationBuilder.CreateIndex(
            name: "IX_spectator_season_wallets_spectator_id",
            table: "spectator_season_wallets",
            column: "spectator_id");

        migrationBuilder.CreateIndex(
            name: "UX_spectator_season_wallets_season_spectator",
            table: "spectator_season_wallets",
            columns: new[] { "season_id", "spectator_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_point_transactions_wallet_created_at",
            table: "point_transactions",
            columns: new[] { "spectator_season_wallet_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "UX_point_transactions_idempotency_key",
            table: "point_transactions",
            column: "idempotency_key",
            unique: true);

        // Tạo ví cho mọi spectator của season đang Active, giữ đúng số dư hiện tại.
        migrationBuilder.Sql(@"
INSERT INTO spectator_season_wallets
(
    season_id,
    spectator_id,
    opening_betting_points,
    current_betting_points,
    season_score,
    final_betting_points,
    final_season_score,
    final_rank,
    status,
    opened_at,
    frozen_at,
    settled_at
)
SELECT
    s.season_id,
    u.user_id,
    1000,
    CASE WHEN u.betting_points < 0 THEN 0 ELSE u.betting_points END,
    COALESCE(SUM(CASE WHEN p.status = 'Evaluated' THEN p.points_awarded ELSE 0 END), 0),
    NULL,
    NULL,
    NULL,
    'Active',
    COALESCE(s.start_date, SYSUTCDATETIME()),
    NULL,
    NULL
FROM seasons s
CROSS JOIN users u
LEFT JOIN tournaments t ON t.season_id = s.season_id
LEFT JOIN races r ON r.tournament_id = t.tournament_id
LEFT JOIN race_predictions p ON p.race_id = r.race_id AND p.spectator_id = u.user_id
WHERE s.status = 'Active'
  AND u.role = 'Spectator'
GROUP BY s.season_id, s.start_date, u.user_id, u.betting_points;
");

        // Backfill lịch sử những season đã có dự đoán để không mất dữ liệu xếp hạng cũ.
        migrationBuilder.Sql(@"
INSERT INTO spectator_season_wallets
(
    season_id,
    spectator_id,
    opening_betting_points,
    current_betting_points,
    season_score,
    final_betting_points,
    final_season_score,
    final_rank,
    status,
    opened_at,
    frozen_at,
    settled_at
)
SELECT
    t.season_id,
    p.spectator_id,
    1000,
    CASE
        WHEN 1000 + SUM(CASE WHEN p.status <> 'Cancelled' THEN p.points_awarded - p.stake_points ELSE 0 END) < 0 THEN 0
        ELSE 1000 + SUM(CASE WHEN p.status <> 'Cancelled' THEN p.points_awarded - p.stake_points ELSE 0 END)
    END,
    SUM(CASE WHEN p.status = 'Evaluated' THEN p.points_awarded ELSE 0 END),
    CASE
        WHEN 1000 + SUM(CASE WHEN p.status <> 'Cancelled' THEN p.points_awarded - p.stake_points ELSE 0 END) < 0 THEN 0
        ELSE 1000 + SUM(CASE WHEN p.status <> 'Cancelled' THEN p.points_awarded - p.stake_points ELSE 0 END)
    END,
    SUM(CASE WHEN p.status = 'Evaluated' THEN p.points_awarded ELSE 0 END),
    NULL,
    'Settled',
    MIN(p.created_at),
    MAX(COALESCE(p.evaluated_at, p.updated_at, p.created_at)),
    MAX(COALESCE(p.evaluated_at, p.updated_at, p.created_at))
FROM race_predictions p
INNER JOIN races r ON r.race_id = p.race_id
INNER JOIN tournaments t ON t.tournament_id = r.tournament_id
INNER JOIN seasons s ON s.season_id = t.season_id
WHERE s.status <> 'Active'
  AND NOT EXISTS
  (
      SELECT 1
      FROM spectator_season_wallets w
      WHERE w.season_id = t.season_id
        AND w.spectator_id = p.spectator_id
  )
GROUP BY t.season_id, p.spectator_id;
");

        migrationBuilder.Sql(@"
INSERT INTO point_transactions
(
    spectator_season_wallet_id,
    transaction_type,
    amount,
    score_delta,
    balance_before,
    balance_after,
    reference_type,
    reference_id,
    idempotency_key,
    description,
    created_at
)
SELECT
    w.spectator_season_wallet_id,
    'SeasonOpening',
    w.opening_betting_points,
    0,
    0,
    w.opening_betting_points,
    'Season',
    w.season_id,
    CONCAT('SEASON_OPENING_', w.season_id, '_', w.spectator_id),
    'Opening balance backfilled by migration.',
    w.opened_at
FROM spectator_season_wallets w;
");

        migrationBuilder.Sql(@"
INSERT INTO point_transactions
(
    spectator_season_wallet_id,
    transaction_type,
    amount,
    score_delta,
    balance_before,
    balance_after,
    reference_type,
    reference_id,
    idempotency_key,
    description,
    created_at
)
SELECT
    w.spectator_season_wallet_id,
    'AdminAdjustment',
    w.current_betting_points - w.opening_betting_points,
    w.season_score,
    w.opening_betting_points,
    w.current_betting_points,
    'Migration',
    NULL,
    CONCAT('LEGACY_BALANCE_', w.spectator_season_wallet_id),
    'Legacy balance and season score backfilled by migration.',
    SYSUTCDATETIME()
FROM spectator_season_wallets w
WHERE w.current_betting_points <> w.opening_betting_points
   OR w.season_score <> 0;
");

        migrationBuilder.Sql(@"
UPDATE season_rewards
SET status = CASE WHEN status = 'Awarded' THEN 'Eligible' ELSE status END,
    claim_deadline = COALESCE(claim_deadline, DATEADD(day, 30, awarded_at));
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "point_transactions");
        migrationBuilder.DropTable(name: "spectator_season_wallets");

        migrationBuilder.DropColumn(name: "admin_note", table: "season_rewards");
        migrationBuilder.DropColumn(name: "approved_at", table: "season_rewards");
        migrationBuilder.DropColumn(name: "claim_deadline", table: "season_rewards");
        migrationBuilder.DropColumn(name: "claimed_at", table: "season_rewards");
        migrationBuilder.DropColumn(name: "delivered_at", table: "season_rewards");
        migrationBuilder.DropColumn(name: "delivery_address", table: "season_rewards");
        migrationBuilder.DropColumn(name: "preparing_at", table: "season_rewards");
        migrationBuilder.DropColumn(name: "receiver_name", table: "season_rewards");
        migrationBuilder.DropColumn(name: "receiver_phone", table: "season_rewards");
        migrationBuilder.DropColumn(name: "rejected_at", table: "season_rewards");

        migrationBuilder.DropCheckConstraint(name: "CK_seasons_status", table: "seasons");
        migrationBuilder.AddCheckConstraint(
            name: "CK_seasons_status",
            table: "seasons",
            sql: "[status] IN ('Draft', 'Active', 'Closed', 'Cancelled')");
    }
}
