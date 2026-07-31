/*
    Fixed-payout cutover reconciliation for the ACTIVE season only.

    Default behavior is dry-run. Set @ApplyChanges = 1 only after reviewing both
    result sets and after applying migration 20260731100000.

    Closed/Settling/Cancelled seasons are intentionally never changed.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @ApplyChanges bit = 0;
DECLARE @Now datetime2 = SYSUTCDATETIME();

IF OBJECT_ID('tempdb..#adjustments') IS NOT NULL DROP TABLE #adjustments;

;WITH RecordedSettlement AS
(
    SELECT
        rp.prediction_id,
        recorded_wallet_delta = COALESCE(SUM(
            CASE
                WHEN pt.requested_amount = 0 AND pt.amount <> 0 THEN pt.amount
                ELSE COALESCE(pt.requested_amount, 0)
            END), 0),
        recorded_score_delta = COALESCE(SUM(COALESCE(pt.score_delta, 0)), 0)
    FROM race_predictions rp
    LEFT JOIN point_transactions pt
        ON pt.reference_type = 'RacePrediction'
       AND pt.reference_id = rp.prediction_id
       AND
       (
           pt.transaction_type IN
           (
               'PredictionPayout',
               'PredictionPayoutReversal',
               'ResultCorrectionAdjustment',
               'PredictionWinSettlement',
               'PredictionLossSettlement',
               'PredictionSettlementReversal'
           )
           OR
           (
               pt.transaction_type = 'AdminAdjustment'
               AND pt.idempotency_key LIKE 'FIXED_PAYOUT_CUTOVER_%'
           )
       )
    GROUP BY rp.prediction_id
)
SELECT
    rp.prediction_id,
    w.spectator_season_wallet_id,
    w.spectator_id,
    w.season_id,
    rp.stake_points,
    rp.is_correct,
    current_points_awarded = rp.points_awarded,
    desired_points_awarded = CASE WHEN rp.is_correct = 1 THEN rp.stake_points * 3 ELSE 0 END,
    wallet_delta_needed =
        CASE WHEN rp.is_correct = 1 THEN rp.stake_points * 3 ELSE 0 END
        - recorded.recorded_wallet_delta,
    score_delta_needed =
        CASE WHEN rp.is_correct = 1 THEN rp.stake_points * 2 ELSE -rp.stake_points END
        - recorded.recorded_score_delta,
    idempotency_key = CONCAT('FIXED_PAYOUT_CUTOVER_', rp.prediction_id)
INTO #adjustments
FROM race_predictions rp
INNER JOIN races r ON r.race_id = rp.race_id
INNER JOIN tournaments t ON t.tournament_id = r.tournament_id
INNER JOIN seasons s ON s.season_id = t.season_id AND s.status = 'Active'
INNER JOIN spectator_season_wallets w
    ON w.season_id = s.season_id
   AND w.spectator_id = rp.spectator_id
INNER JOIN RecordedSettlement recorded ON recorded.prediction_id = rp.prediction_id
WHERE rp.status = 'Evaluated'
  AND rp.is_correct IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM point_transactions applied
      WHERE applied.idempotency_key = CONCAT('FIXED_PAYOUT_CUTOVER_', rp.prediction_id)
  );

SELECT
    mode = CASE WHEN @ApplyChanges = 1 THEN 'APPLY' ELSE 'DRY RUN' END,
    prediction_id,
    spectator_id,
    season_id,
    stake_points,
    is_correct,
    current_points_awarded,
    desired_points_awarded,
    wallet_delta_needed,
    score_delta_needed,
    idempotency_key
FROM #adjustments
ORDER BY season_id, spectator_id, prediction_id;

SELECT
    mode = CASE WHEN @ApplyChanges = 1 THEN 'APPLY' ELSE 'DRY RUN' END,
    spectator_id,
    season_id,
    affected_predictions = COUNT(*),
    requested_wallet_change = SUM(wallet_delta_needed),
    season_score_change = SUM(score_delta_needed)
FROM #adjustments
GROUP BY spectator_id, season_id
ORDER BY season_id, spectator_id;

IF @ApplyChanges = 0
BEGIN
    PRINT 'Dry-run only. No data was changed.';
    RETURN;
END;

BEGIN TRANSACTION;

DECLARE
    @PredictionId int,
    @WalletId int,
    @SpectatorId int,
    @DesiredPointsAwarded int,
    @WalletDelta int,
    @ScoreDelta int,
    @IdempotencyKey varchar(150),
    @BalanceBefore int,
    @BalanceAfter int,
    @DebtBefore int,
    @DebtDelta int,
    @ActualAmount int,
    @RecoveredDebt int,
    @RecoverNow int;

DECLARE adjustment_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT
    prediction_id,
    spectator_season_wallet_id,
    spectator_id,
    desired_points_awarded,
    wallet_delta_needed,
    score_delta_needed,
    idempotency_key
FROM #adjustments
ORDER BY spectator_season_wallet_id, prediction_id;

OPEN adjustment_cursor;
FETCH NEXT FROM adjustment_cursor INTO
    @PredictionId, @WalletId, @SpectatorId, @DesiredPointsAwarded,
    @WalletDelta, @ScoreDelta, @IdempotencyKey;

WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT
        @BalanceBefore = current_betting_points,
        @DebtBefore = pending_recovery_points
    FROM spectator_season_wallets WITH (UPDLOCK, HOLDLOCK)
    WHERE spectator_season_wallet_id = @WalletId
      AND status = 'Active';

    IF @BalanceBefore IS NULL
        THROW 51000, 'Active spectator wallet was not found during reconciliation.', 1;

    SET @DebtDelta = 0;
    SET @ActualAmount = @WalletDelta;

    IF @WalletDelta > 0 AND @DebtBefore > 0
    BEGIN
        SET @RecoveredDebt = CASE WHEN @WalletDelta < @DebtBefore THEN @WalletDelta ELSE @DebtBefore END;
        SET @ActualAmount = @WalletDelta - @RecoveredDebt;
        SET @DebtDelta = -@RecoveredDebt;
    END;
    ELSE IF @WalletDelta < 0
    BEGIN
        SET @RecoverNow = CASE WHEN @BalanceBefore < -@WalletDelta THEN @BalanceBefore ELSE -@WalletDelta END;
        SET @ActualAmount = -@RecoverNow;
        SET @DebtDelta = (-@WalletDelta) - @RecoverNow;
    END;

    SET @BalanceAfter = @BalanceBefore + @ActualAmount;

    UPDATE spectator_season_wallets
    SET current_betting_points = @BalanceAfter,
        season_score = season_score + @ScoreDelta,
        pending_recovery_points = pending_recovery_points + @DebtDelta
    WHERE spectator_season_wallet_id = @WalletId;

    UPDATE users
    SET betting_points = @BalanceAfter,
        updated_at = @Now
    WHERE user_id = @SpectatorId;

    UPDATE race_predictions
    SET points_awarded = @DesiredPointsAwarded,
        updated_at = @Now
    WHERE prediction_id = @PredictionId;

    INSERT INTO point_transactions
    (
        spectator_season_wallet_id,
        transaction_type,
        requested_amount,
        amount,
        score_delta,
        recovery_debt_delta,
        balance_before,
        balance_after,
        reference_type,
        reference_id,
        idempotency_key,
        description,
        created_at
    )
    VALUES
    (
        @WalletId,
        'AdminAdjustment',
        @WalletDelta,
        @ActualAmount,
        @ScoreDelta,
        @DebtDelta,
        @BalanceBefore,
        @BalanceAfter,
        'RacePrediction',
        @PredictionId,
        @IdempotencyKey,
        'Audited active-season cutover from pari-mutuel to fixed prediction payout.',
        @Now
    );

    SET @BalanceBefore = NULL;
    FETCH NEXT FROM adjustment_cursor INTO
        @PredictionId, @WalletId, @SpectatorId, @DesiredPointsAwarded,
        @WalletDelta, @ScoreDelta, @IdempotencyKey;
END;

CLOSE adjustment_cursor;
DEALLOCATE adjustment_cursor;

COMMIT TRANSACTION;

SELECT
    w.season_id,
    w.spectator_id,
    w.current_betting_points,
    w.season_score,
    w.pending_recovery_points
FROM spectator_season_wallets w
INNER JOIN seasons s ON s.season_id = w.season_id AND s.status = 'Active'
WHERE EXISTS
(
    SELECT 1 FROM #adjustments a
    WHERE a.spectator_season_wallet_id = w.spectator_season_wallet_id
)
ORDER BY w.season_id, w.spectator_id;
