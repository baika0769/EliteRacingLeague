USE EliteRacingLeague;
GO

SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.prize_awards', N'U') IS NULL
    BEGIN
        THROW 51001, N'Không tìm thấy bảng dbo.prize_awards.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE [name] = N'CK_prize_awards_status'
          AND [parent_object_id] = OBJECT_ID(N'dbo.prize_awards')
    )
    BEGIN
        ALTER TABLE dbo.prize_awards
        DROP CONSTRAINT CK_prize_awards_status;
    END;

    UPDATE dbo.prize_awards
    SET [status] = 'ReadyToClaim'
    WHERE [status] = 'Pending';

    UPDATE dbo.prize_awards
    SET [status] = 'Rejected'
    WHERE [status] = 'Cancelled';

    ALTER TABLE dbo.prize_awards WITH CHECK
    ADD CONSTRAINT CK_prize_awards_status
    CHECK ([status] IN ('ReadyToClaim', 'UnderReview', 'Paid', 'Rejected'));

    ALTER TABLE dbo.prize_awards
    CHECK CONSTRAINT CK_prize_awards_status;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1
           FROM dbo.__EFMigrationsHistory
           WHERE MigrationId = N'20260720001000_ExpandPrizeAwardStatuses'
       )
    BEGIN
        DECLARE @ProductVersion nvarchar(32);

        SELECT TOP (1)
            @ProductVersion = ProductVersion
        FROM dbo.__EFMigrationsHistory
        ORDER BY MigrationId DESC;

        SET @ProductVersion = COALESCE(@ProductVersion, N'8.0.22');

        INSERT INTO dbo.__EFMigrationsHistory(MigrationId, ProductVersion)
        VALUES
        (
            N'20260720001000_ExpandPrizeAwardStatuses',
            @ProductVersion
        );
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO

SELECT
    cc.name AS constraint_name,
    cc.definition
FROM sys.check_constraints AS cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.prize_awards');
GO
