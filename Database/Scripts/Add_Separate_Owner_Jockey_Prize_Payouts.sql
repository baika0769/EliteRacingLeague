USE EliteRacingLeague;
GO

SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.prize_payouts', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.prize_payouts
        (
            prize_payout_id INT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_prize_payouts PRIMARY KEY,
            prize_award_id INT NOT NULL,
            recipient_user_id INT NOT NULL,
            recipient_type VARCHAR(20) NOT NULL,
            amount DECIMAL(18,2) NOT NULL,
            status VARCHAR(30) NOT NULL,
            claimed_at DATETIME2 NULL,
            paid_at DATETIME2 NULL,
            rejected_at DATETIME2 NULL,
            payment_reference NVARCHAR(200) NULL,
            admin_note NVARCHAR(1000) NULL,
            created_at DATETIME2 NOT NULL
                CONSTRAINT DF_prize_payouts_created_at DEFAULT SYSUTCDATETIME(),
            updated_at DATETIME2 NULL,

            CONSTRAINT FK_prize_payouts_prize_awards
                FOREIGN KEY (prize_award_id)
                REFERENCES dbo.prize_awards(prize_award_id)
                ON DELETE CASCADE,

            CONSTRAINT FK_prize_payouts_users
                FOREIGN KEY (recipient_user_id)
                REFERENCES dbo.users(user_id),

            CONSTRAINT CK_prize_payouts_recipient_type
                CHECK (recipient_type IN ('Owner', 'Jockey')),

            CONSTRAINT CK_prize_payouts_status
                CHECK (status IN ('ReadyToClaim', 'UnderReview', 'Paid', 'Rejected')),

            CONSTRAINT CK_prize_payouts_amount
                CHECK (amount >= 0)
        );

        CREATE UNIQUE INDEX UQ_prize_payouts_award_recipient_type
            ON dbo.prize_payouts(prize_award_id, recipient_type);

        CREATE INDEX IX_prize_payouts_recipient_status
            ON dbo.prize_payouts(recipient_user_id, recipient_type, status);
    END;

    /*
      Existing data migration:
      - PrizeAward.PrizeAmount remains the total purse for the finishing position.
      - Owner receives 80%; jockey receives the remaining 20%.
      - If no jockey was assigned, owner receives 100%.
      - The legacy owner status is preserved.
      - Existing jockeys start at ReadyToClaim because the old system never paid them separately.
    */
    INSERT INTO dbo.prize_payouts
    (
        prize_award_id,
        recipient_user_id,
        recipient_type,
        amount,
        status,
        claimed_at,
        paid_at,
        rejected_at,
        payment_reference,
        admin_note,
        created_at,
        updated_at
    )
    SELECT
        pa.prize_award_id,
        pa.owner_id,
        'Owner',
        CASE
            WHEN pa.jockey_id IS NULL THEN pa.prize_amount
            ELSE ROUND(pa.prize_amount * 0.80, 2)
        END,
        pa.status,
        CASE WHEN pa.status IN ('UnderReview', 'Paid') THEN pa.created_at ELSE NULL END,
        CASE WHEN pa.status = 'Paid' THEN pa.paid_at ELSE NULL END,
        CASE WHEN pa.status = 'Rejected' THEN pa.created_at ELSE NULL END,
        CASE WHEN pa.status = 'Paid' THEN CONCAT('LEGACY-AWARD-', pa.prize_award_id) ELSE NULL END,
        N'Migrated from the legacy shared PrizeAward workflow.',
        pa.created_at,
        SYSUTCDATETIME()
    FROM dbo.prize_awards pa
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.prize_payouts pp
        WHERE pp.prize_award_id = pa.prize_award_id
          AND pp.recipient_type = 'Owner'
    );

    INSERT INTO dbo.prize_payouts
    (
        prize_award_id,
        recipient_user_id,
        recipient_type,
        amount,
        status,
        claimed_at,
        paid_at,
        rejected_at,
        payment_reference,
        admin_note,
        created_at,
        updated_at
    )
    SELECT
        pa.prize_award_id,
        pa.jockey_id,
        'Jockey',
        pa.prize_amount - ROUND(pa.prize_amount * 0.80, 2),
        'ReadyToClaim',
        NULL,
        NULL,
        NULL,
        NULL,
        N'Created during migration because the legacy system had no separate jockey payout.',
        pa.created_at,
        SYSUTCDATETIME()
    FROM dbo.prize_awards pa
    WHERE pa.jockey_id IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.prize_payouts pp
          WHERE pp.prize_award_id = pa.prize_award_id
            AND pp.recipient_type = 'Jockey'
      );

    /* Rebuild the legacy aggregate status from the two independent payouts. */
    UPDATE pa
    SET
        status = CASE
            WHEN summary.total_count = summary.paid_count + summary.rejected_count
                 AND summary.paid_count > 0 THEN 'Paid'
            WHEN summary.total_count = summary.rejected_count THEN 'Rejected'
            WHEN summary.review_count > 0 THEN 'UnderReview'
            ELSE 'ReadyToClaim'
        END,
        paid_at = CASE
            WHEN summary.total_count = summary.paid_count + summary.rejected_count
                 AND summary.paid_count > 0 THEN summary.last_paid_at
            ELSE NULL
        END
    FROM dbo.prize_awards pa
    CROSS APPLY
    (
        SELECT
            COUNT(*) AS total_count,
            SUM(CASE WHEN pp.status = 'Paid' THEN 1 ELSE 0 END) AS paid_count,
            SUM(CASE WHEN pp.status = 'Rejected' THEN 1 ELSE 0 END) AS rejected_count,
            SUM(CASE WHEN pp.status = 'UnderReview' THEN 1 ELSE 0 END) AS review_count,
            MAX(pp.paid_at) AS last_paid_at
        FROM dbo.prize_payouts pp
        WHERE pp.prize_award_id = pa.prize_award_id
    ) summary
    WHERE summary.total_count > 0;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO

SELECT
    pa.prize_award_id,
    pa.prize_amount AS total_prize_amount,
    pp.prize_payout_id,
    pp.recipient_type,
    pp.recipient_user_id,
    pp.amount,
    pp.status,
    pp.payment_reference
FROM dbo.prize_awards pa
JOIN dbo.prize_payouts pp
    ON pp.prize_award_id = pa.prize_award_id
ORDER BY pa.prize_award_id, pp.recipient_type;
GO
