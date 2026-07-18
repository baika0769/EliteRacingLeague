using Eliteracingleague.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eliteracingleague.API.Migrations;

[DbContext(typeof(EliteRacingLeagueContext))]
[Migration("20260719120000_CompleteRaceLifecycleAndProductionHardening")]
public partial class CompleteRaceLifecycleAndProductionHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        SET XACT_ABORT ON;

        /* One tournament may contain multiple races.
           Remove only the legacy single-column UNIQUE constraint/index on tournament_id.
           A UNIQUE constraint must be removed with ALTER TABLE DROP CONSTRAINT;
           a standalone UNIQUE index must be removed with DROP INDEX. */
        DECLARE @raceObjectId int = OBJECT_ID(N'dbo.races');
        DECLARE @raceConstraint sysname;
        DECLARE @raceIndex sysname;
        DECLARE @dropSql nvarchar(max);

        /* Drop a legacy UNIQUE KEY constraint on races(tournament_id), if present. */
        SELECT TOP (1) @raceConstraint = kc.name
        FROM sys.key_constraints kc
        JOIN sys.indexes i
          ON i.object_id = kc.parent_object_id
         AND i.index_id = kc.unique_index_id
        WHERE kc.parent_object_id = @raceObjectId
          AND kc.[type] = N'UQ'
          AND 1 = (
              SELECT COUNT(*)
              FROM sys.index_columns ic
              WHERE ic.object_id = i.object_id
                AND ic.index_id = i.index_id
                AND ic.key_ordinal > 0
          )
          AND EXISTS (
              SELECT 1
              FROM sys.index_columns ic
              JOIN sys.columns c
                ON c.object_id = ic.object_id
               AND c.column_id = ic.column_id
              WHERE ic.object_id = i.object_id
                AND ic.index_id = i.index_id
                AND ic.key_ordinal = 1
                AND c.name = N'tournament_id'
          );

        IF @raceConstraint IS NOT NULL
        BEGIN
            SET @dropSql = N'ALTER TABLE [dbo].[races] DROP CONSTRAINT ' + QUOTENAME(@raceConstraint) + N';';
            EXEC sys.sp_executesql @dropSql;
        END;

        /* Drop standalone legacy UNIQUE indexes on races(tournament_id), if present. */
        WHILE 1 = 1
        BEGIN
            SET @raceIndex = NULL;

            SELECT TOP (1) @raceIndex = i.name
            FROM sys.indexes i
            WHERE i.object_id = @raceObjectId
              AND i.is_unique = 1
              AND i.is_primary_key = 0
              AND i.is_unique_constraint = 0
              AND 1 = (
                  SELECT COUNT(*)
                  FROM sys.index_columns ic
                  WHERE ic.object_id = i.object_id
                    AND ic.index_id = i.index_id
                    AND ic.key_ordinal > 0
              )
              AND EXISTS (
                  SELECT 1
                  FROM sys.index_columns ic
                  JOIN sys.columns c
                    ON c.object_id = ic.object_id
                   AND c.column_id = ic.column_id
                  WHERE ic.object_id = i.object_id
                    AND ic.index_id = i.index_id
                    AND ic.key_ordinal = 1
                    AND c.name = N'tournament_id'
              );

            IF @raceIndex IS NULL BREAK;

            SET @dropSql = N'DROP INDEX ' + QUOTENAME(@raceIndex) + N' ON [dbo].[races];';
            EXEC sys.sp_executesql @dropSql;
        END;

        /* Keep a normal non-unique lookup index for tournament -> races. */
        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE object_id = @raceObjectId
              AND name = N'IX_races_tournament_id'
        )
            CREATE INDEX IX_races_tournament_id ON dbo.races(tournament_id);

        IF COL_LENGTH('dbo.races','original_race_date') IS NULL ALTER TABLE dbo.races ADD original_race_date datetime2 NULL;
        IF COL_LENGTH('dbo.races','postponed_at') IS NULL ALTER TABLE dbo.races ADD postponed_at datetime2 NULL;
        IF COL_LENGTH('dbo.races','postponement_reason') IS NULL ALTER TABLE dbo.races ADD postponement_reason nvarchar(1000) NULL;
        IF COL_LENGTH('dbo.races','cancelled_at') IS NULL ALTER TABLE dbo.races ADD cancelled_at datetime2 NULL;
        IF COL_LENGTH('dbo.races','cancellation_reason') IS NULL ALTER TABLE dbo.races ADD cancellation_reason nvarchar(1000) NULL;
        IF COL_LENGTH('dbo.races','lifecycle_version') IS NULL ALTER TABLE dbo.races ADD lifecycle_version int NOT NULL CONSTRAINT DF_races_lifecycle_version DEFAULT(0);
        IF COL_LENGTH('dbo.races','row_version') IS NULL ALTER TABLE dbo.races ADD row_version rowversion NOT NULL;
        EXEC sys.sp_executesql N'UPDATE dbo.races SET original_race_date = race_date WHERE original_race_date IS NULL;';

        IF COL_LENGTH('dbo.race_results','outcome_status') IS NULL ALTER TABLE dbo.race_results ADD outcome_status varchar(30) NOT NULL CONSTRAINT DF_race_results_outcome DEFAULT('Finished');
        IF COL_LENGTH('dbo.race_results','revision_number') IS NULL ALTER TABLE dbo.race_results ADD revision_number int NOT NULL CONSTRAINT DF_race_results_revision DEFAULT(0);
        IF COL_LENGTH('dbo.race_results','row_version') IS NULL ALTER TABLE dbo.race_results ADD row_version rowversion NOT NULL;

        IF COL_LENGTH('dbo.race_registrations','withdrawal_reason') IS NULL ALTER TABLE dbo.race_registrations ADD withdrawal_reason nvarchar(500) NULL;
        IF COL_LENGTH('dbo.race_registrations','withdrawn_at') IS NULL ALTER TABLE dbo.race_registrations ADD withdrawn_at datetime2 NULL;
        IF COL_LENGTH('dbo.race_registrations','withdrawn_by_user_id') IS NULL ALTER TABLE dbo.race_registrations ADD withdrawn_by_user_id int NULL;
        IF COL_LENGTH('dbo.race_registrations','row_version') IS NULL ALTER TABLE dbo.race_registrations ADD row_version rowversion NOT NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_race_registrations_withdrawn_by')
            EXEC sys.sp_executesql N'ALTER TABLE dbo.race_registrations ADD CONSTRAINT FK_race_registrations_withdrawn_by FOREIGN KEY(withdrawn_by_user_id) REFERENCES dbo.users(user_id);';

        IF COL_LENGTH('dbo.jockey_invitations','expires_at') IS NULL ALTER TABLE dbo.jockey_invitations ADD expires_at datetime2 NULL;
        IF COL_LENGTH('dbo.jockey_invitations','response_note') IS NULL ALTER TABLE dbo.jockey_invitations ADD response_note nvarchar(500) NULL;
        IF COL_LENGTH('dbo.jockey_invitations','row_version') IS NULL ALTER TABLE dbo.jockey_invitations ADD row_version rowversion NOT NULL;

        IF COL_LENGTH('dbo.spectator_season_wallets','pending_recovery_points') IS NULL ALTER TABLE dbo.spectator_season_wallets ADD pending_recovery_points int NOT NULL CONSTRAINT DF_wallet_pending_recovery DEFAULT(0);
        IF COL_LENGTH('dbo.point_transactions','requested_amount') IS NULL ALTER TABLE dbo.point_transactions ADD requested_amount int NOT NULL CONSTRAINT DF_point_tx_requested DEFAULT(0);
        IF COL_LENGTH('dbo.point_transactions','recovery_debt_delta') IS NULL ALTER TABLE dbo.point_transactions ADD recovery_debt_delta int NOT NULL CONSTRAINT DF_point_tx_debt DEFAULT(0);

        IF COL_LENGTH('dbo.season_reward_rules','reward_item_id') IS NULL ALTER TABLE dbo.season_reward_rules ADD reward_item_id int NULL;
        IF COL_LENGTH('dbo.season_reward_rules','quantity') IS NULL ALTER TABLE dbo.season_reward_rules ADD quantity int NOT NULL CONSTRAINT DF_reward_rules_quantity DEFAULT(1);
        IF COL_LENGTH('dbo.season_rewards','reward_item_id') IS NULL ALTER TABLE dbo.season_rewards ADD reward_item_id int NULL;
        IF COL_LENGTH('dbo.season_rewards','quantity') IS NULL ALTER TABLE dbo.season_rewards ADD quantity int NOT NULL CONSTRAINT DF_rewards_quantity DEFAULT(1);
        IF COL_LENGTH('dbo.season_rewards','inventory_reserved') IS NULL ALTER TABLE dbo.season_rewards ADD inventory_reserved bit NOT NULL CONSTRAINT DF_rewards_reserved DEFAULT(0);

        IF COL_LENGTH('dbo.users','token_version') IS NULL ALTER TABLE dbo.users ADD token_version int NOT NULL CONSTRAINT DF_users_token_version DEFAULT(0);
        IF COL_LENGTH('dbo.users','last_login_at') IS NULL ALTER TABLE dbo.users ADD last_login_at datetime2 NULL;
        IF COL_LENGTH('dbo.users','failed_login_attempts') IS NULL ALTER TABLE dbo.users ADD failed_login_attempts int NOT NULL CONSTRAINT DF_users_failed_login DEFAULT(0);
        IF COL_LENGTH('dbo.users','lockout_end_at') IS NULL ALTER TABLE dbo.users ADD lockout_end_at datetime2 NULL;

        IF OBJECT_ID(N'dbo.audit_logs', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.audit_logs(
                audit_log_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_audit_logs PRIMARY KEY,
                user_id int NULL,
                action varchar(80) NOT NULL,
                entity_type varchar(100) NOT NULL,
                entity_id varchar(100) NULL,
                old_values_json nvarchar(max) NULL,
                new_values_json nvarchar(max) NULL,
                reason nvarchar(1000) NULL,
                ip_address varchar(64) NULL,
                user_agent nvarchar(500) NULL,
                correlation_id varchar(100) NOT NULL,
                created_at datetime2 NOT NULL CONSTRAINT DF_audit_logs_created DEFAULT(sysutcdatetime()),
                CONSTRAINT FK_audit_logs_users FOREIGN KEY(user_id) REFERENCES dbo.users(user_id) ON DELETE SET NULL
            );
            CREATE INDEX IX_audit_logs_entity ON dbo.audit_logs(entity_type, entity_id, created_at);
            CREATE INDEX IX_audit_logs_correlation ON dbo.audit_logs(correlation_id);
        END;

        IF OBJECT_ID(N'dbo.race_result_revisions', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.race_result_revisions(
                race_result_revision_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_race_result_revisions PRIMARY KEY,
                race_id int NOT NULL,
                result_id int NULL,
                registration_id int NOT NULL,
                version_number int NOT NULL,
                change_type varchar(50) NOT NULL,
                snapshot_json nvarchar(max) NOT NULL,
                reason nvarchar(1000) NOT NULL,
                changed_by_user_id int NOT NULL,
                created_at datetime2 NOT NULL CONSTRAINT DF_result_revisions_created DEFAULT(sysutcdatetime()),
                CONSTRAINT FK_result_revisions_races FOREIGN KEY(race_id) REFERENCES dbo.races(race_id) ON DELETE CASCADE,
                CONSTRAINT FK_result_revisions_results FOREIGN KEY(result_id) REFERENCES dbo.race_results(result_id),
                CONSTRAINT FK_result_revisions_registrations FOREIGN KEY(registration_id) REFERENCES dbo.race_registrations(registration_id),
                CONSTRAINT FK_result_revisions_users FOREIGN KEY(changed_by_user_id) REFERENCES dbo.users(user_id)
            );
            CREATE INDEX IX_result_revisions_race_version ON dbo.race_result_revisions(race_id, version_number);
        END;

        IF OBJECT_ID(N'dbo.tournament_standings', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.tournament_standings(
                tournament_standing_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_tournament_standings PRIMARY KEY,
                tournament_id int NOT NULL,
                horse_id int NOT NULL,
                owner_id int NOT NULL,
                jockey_id int NULL,
                total_points int NOT NULL,
                wins int NOT NULL,
                second_places int NOT NULL,
                third_places int NOT NULL,
                completed_races int NOT NULL,
                total_finish_time_seconds decimal(18,3) NOT NULL,
                final_rank int NOT NULL,
                is_final bit NOT NULL,
                calculated_at datetime2 NOT NULL,
                finalized_at datetime2 NULL,
                CONSTRAINT FK_tournament_standings_tournaments FOREIGN KEY(tournament_id) REFERENCES dbo.tournaments(tournament_id) ON DELETE CASCADE,
                CONSTRAINT FK_tournament_standings_horses FOREIGN KEY(horse_id) REFERENCES dbo.horses(horse_id),
                CONSTRAINT FK_tournament_standings_owners FOREIGN KEY(owner_id) REFERENCES dbo.horse_owners(owner_id),
                CONSTRAINT FK_tournament_standings_jockeys FOREIGN KEY(jockey_id) REFERENCES dbo.jockeys(jockey_id)
            );
            CREATE UNIQUE INDEX UX_tournament_standings_horse ON dbo.tournament_standings(tournament_id, horse_id);
            CREATE UNIQUE INDEX UX_tournament_standings_rank ON dbo.tournament_standings(tournament_id, final_rank);
        END;

        IF OBJECT_ID(N'dbo.reward_items', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.reward_items(
                reward_item_id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_reward_items PRIMARY KEY,
                name nvarchar(200) NOT NULL,
                description nvarchar(1000) NULL,
                image_url nvarchar(500) NULL,
                sku varchar(80) NOT NULL,
                stock_quantity int NOT NULL,
                reserved_quantity int NOT NULL,
                delivered_quantity int NOT NULL,
                is_active bit NOT NULL,
                created_at datetime2 NOT NULL,
                updated_at datetime2 NULL,
                row_version rowversion NOT NULL,
                CONSTRAINT CK_reward_items_quantities CHECK(stock_quantity >= 0 AND reserved_quantity >= 0 AND delivered_quantity >= 0 AND reserved_quantity + delivered_quantity <= stock_quantity)
            );
            CREATE UNIQUE INDEX UX_reward_items_sku ON dbo.reward_items(sku);
        END;

        IF OBJECT_ID(N'dbo.reward_inventory_transactions', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.reward_inventory_transactions(
                reward_inventory_transaction_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_reward_inventory_transactions PRIMARY KEY,
                reward_item_id int NOT NULL,
                quantity_delta int NOT NULL,
                reserved_delta int NOT NULL,
                delivered_delta int NOT NULL,
                transaction_type varchar(40) NOT NULL,
                reference_type varchar(50) NULL,
                reference_id int NULL,
                idempotency_key varchar(180) NOT NULL,
                note nvarchar(500) NULL,
                created_by_user_id int NOT NULL,
                created_at datetime2 NOT NULL,
                CONSTRAINT FK_reward_inventory_item FOREIGN KEY(reward_item_id) REFERENCES dbo.reward_items(reward_item_id) ON DELETE CASCADE,
                CONSTRAINT FK_reward_inventory_user FOREIGN KEY(created_by_user_id) REFERENCES dbo.users(user_id)
            );
            CREATE UNIQUE INDEX UX_reward_inventory_idempotency ON dbo.reward_inventory_transactions(idempotency_key);
        END;

        IF OBJECT_ID(N'dbo.password_reset_tokens', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.password_reset_tokens(
                password_reset_token_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_password_reset_tokens PRIMARY KEY,
                user_id int NOT NULL,
                token_hash varchar(128) NOT NULL,
                expires_at datetime2 NOT NULL,
                is_used bit NOT NULL,
                created_at datetime2 NOT NULL,
                used_at datetime2 NULL,
                requested_ip varchar(64) NULL,
                CONSTRAINT FK_password_reset_tokens_users FOREIGN KEY(user_id) REFERENCES dbo.users(user_id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX UX_password_reset_tokens_hash ON dbo.password_reset_tokens(token_hash);
            CREATE INDEX IX_password_reset_tokens_user_status ON dbo.password_reset_tokens(user_id, is_used, expires_at);
        END;

        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_reward_rules_items')
            EXEC sys.sp_executesql N'ALTER TABLE dbo.season_reward_rules ADD CONSTRAINT FK_reward_rules_items FOREIGN KEY(reward_item_id) REFERENCES dbo.reward_items(reward_item_id) ON DELETE SET NULL;';
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_season_rewards_items')
            EXEC sys.sp_executesql N'ALTER TABLE dbo.season_rewards ADD CONSTRAINT FK_season_rewards_items FOREIGN KEY(reward_item_id) REFERENCES dbo.reward_items(reward_item_id) ON DELETE SET NULL;';

        EXEC sys.sp_executesql N'
        UPDATE rr
        SET outcome_status = ''Disqualified'', finish_position = NULL, finish_time_seconds = NULL
        FROM dbo.race_results rr
        WHERE EXISTS (
            SELECT 1 FROM dbo.race_violations rv
            WHERE rv.registration_id = rr.registration_id AND LOWER(ISNULL(rv.action,'''')) LIKE ''%disqual%''
        );';
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        SET XACT_ABORT ON;
        IF OBJECT_ID(N'dbo.password_reset_tokens', N'U') IS NOT NULL DROP TABLE dbo.password_reset_tokens;
        IF OBJECT_ID(N'dbo.reward_inventory_transactions', N'U') IS NOT NULL DROP TABLE dbo.reward_inventory_transactions;
        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_reward_rules_items') ALTER TABLE dbo.season_reward_rules DROP CONSTRAINT FK_reward_rules_items;
        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_season_rewards_items') ALTER TABLE dbo.season_rewards DROP CONSTRAINT FK_season_rewards_items;
        IF OBJECT_ID(N'dbo.reward_items', N'U') IS NOT NULL DROP TABLE dbo.reward_items;
        IF OBJECT_ID(N'dbo.tournament_standings', N'U') IS NOT NULL DROP TABLE dbo.tournament_standings;
        IF OBJECT_ID(N'dbo.race_result_revisions', N'U') IS NOT NULL DROP TABLE dbo.race_result_revisions;
        IF OBJECT_ID(N'dbo.audit_logs', N'U') IS NOT NULL DROP TABLE dbo.audit_logs;
        """);
    }
}
