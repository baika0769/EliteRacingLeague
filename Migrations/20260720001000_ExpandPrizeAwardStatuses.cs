using Eliteracingleague.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eliteracingleague.API.Migrations;

[DbContext(typeof(EliteRacingLeagueContext))]
[Migration("20260720001000_ExpandPrizeAwardStatuses")]
public partial class ExpandPrizeAwardStatuses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.prize_awards', N'U') IS NOT NULL
            BEGIN
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
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.prize_awards', N'U') IS NOT NULL
            BEGIN
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
                SET [status] = 'Pending'
                WHERE [status] IN ('ReadyToClaim', 'UnderReview');

                UPDATE dbo.prize_awards
                SET [status] = 'Cancelled'
                WHERE [status] = 'Rejected';

                ALTER TABLE dbo.prize_awards WITH CHECK
                ADD CONSTRAINT CK_prize_awards_status
                CHECK ([status] IN ('Pending', 'Paid', 'Cancelled'));

                ALTER TABLE dbo.prize_awards
                CHECK CONSTRAINT CK_prize_awards_status;
            END;
            """);
    }
}
