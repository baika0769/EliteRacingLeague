using Eliteracingleague.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eliteracingleague.API.Migrations;

[DbContext(typeof(EliteRacingLeagueContext))]
[Migration("20260722030000_AddSeasonRewardDeliveryConfirmation")]
public partial class AddSeasonRewardDeliveryConfirmation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "shipped_at",
            table: "season_rewards",
            type: "datetime2",
            nullable: true);

        // Normalize the active season's historical leaderboard score. Wallet payout
        // remains stake-based, but every correct prediction contributes the same
        // fixed score configured by its season.
        migrationBuilder.Sql(@"
UPDATE pt
SET pt.score_delta = s.points_per_correct_prediction
FROM point_transactions pt
INNER JOIN spectator_season_wallets w
    ON w.spectator_season_wallet_id = pt.spectator_season_wallet_id
INNER JOIN seasons s ON s.season_id = w.season_id
WHERE s.status = 'Active'
  AND pt.transaction_type = 'PredictionPayout'
  AND pt.reference_type = 'RacePrediction';

UPDATE pt
SET pt.score_delta = -s.points_per_correct_prediction
FROM point_transactions pt
INNER JOIN spectator_season_wallets w
    ON w.spectator_season_wallet_id = pt.spectator_season_wallet_id
INNER JOIN seasons s ON s.season_id = w.season_id
WHERE s.status = 'Active'
  AND pt.transaction_type IN ('PredictionPayoutReversal', 'ResultCorrectionAdjustment')
  AND pt.reference_type = 'RacePrediction';

UPDATE pt
SET pt.score_delta = fair.correct_count * s.points_per_correct_prediction
FROM point_transactions pt
INNER JOIN spectator_season_wallets w
    ON w.spectator_season_wallet_id = pt.spectator_season_wallet_id
INNER JOIN seasons s ON s.season_id = w.season_id
OUTER APPLY
(
    SELECT COUNT(*) AS correct_count
    FROM race_predictions rp
    INNER JOIN races r ON r.race_id = rp.race_id
    INNER JOIN tournaments t ON t.tournament_id = r.tournament_id
    WHERE t.season_id = w.season_id
      AND rp.spectator_id = w.spectator_id
      AND rp.status = 'Evaluated'
      AND rp.is_correct = 1
      AND NOT EXISTS
      (
          SELECT 1
          FROM point_transactions paid
          WHERE paid.spectator_season_wallet_id = w.spectator_season_wallet_id
            AND paid.transaction_type = 'PredictionPayout'
            AND paid.reference_type = 'RacePrediction'
            AND paid.reference_id = rp.prediction_id
      )
) fair
WHERE s.status = 'Active'
  AND pt.transaction_type = 'AdminAdjustment'
  AND pt.reference_type = 'Migration'
  AND pt.idempotency_key LIKE 'LEGACY_BALANCE_%';

UPDATE w
SET w.season_score = CASE WHEN totals.score_total < 0 THEN 0 ELSE totals.score_total END
FROM spectator_season_wallets w
INNER JOIN seasons s ON s.season_id = w.season_id
OUTER APPLY
(
    SELECT COALESCE(SUM(pt.score_delta), 0) AS score_total
    FROM point_transactions pt
    WHERE pt.spectator_season_wallet_id = w.spectator_season_wallet_id
) totals
WHERE s.status = 'Active';
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "shipped_at",
            table: "season_rewards");
    }
}
