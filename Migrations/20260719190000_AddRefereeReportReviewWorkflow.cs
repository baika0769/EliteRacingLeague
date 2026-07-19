using Eliteracingleague.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eliteracingleague.API.Migrations;

[DbContext(typeof(EliteRacingLeagueContext))]
[Migration("20260719190000_AddRefereeReportReviewWorkflow")]
public partial class AddRefereeReportReviewWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "status",
            table: "referee_reports",
            type: "varchar(30)",
            unicode: false,
            maxLength: 30,
            nullable: false,
            defaultValue: "Submitted");

        migrationBuilder.AddColumn<string>(
            name: "return_reason_category",
            table: "referee_reports",
            type: "varchar(50)",
            unicode: false,
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "return_reason",
            table: "referee_reports",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "reviewed_by_admin_id",
            table: "referee_reports",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "reviewed_at",
            table: "referee_reports",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "revision_number",
            table: "referee_reports",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<DateTime>(
            name: "resubmitted_at",
            table: "referee_reports",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "updated_at",
            table: "referee_reports",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_referee_reports_status",
            table: "referee_reports",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_referee_reports_reviewed_by_admin_id",
            table: "referee_reports",
            column: "reviewed_by_admin_id");

        migrationBuilder.AddCheckConstraint(
            name: "CK_referee_reports_status",
            table: "referee_reports",
            sql: "[status] IN ('Submitted', 'Returned', 'Approved')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_referee_reports_revision_number",
            table: "referee_reports",
            sql: "[revision_number] >= 1");

        migrationBuilder.AddForeignKey(
            name: "FK_referee_reports_reviewed_by_admin",
            table: "referee_reports",
            column: "reviewed_by_admin_id",
            principalTable: "users",
            principalColumn: "user_id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_referee_reports_reviewed_by_admin",
            table: "referee_reports");

        migrationBuilder.DropCheckConstraint(
            name: "CK_referee_reports_revision_number",
            table: "referee_reports");

        migrationBuilder.DropCheckConstraint(
            name: "CK_referee_reports_status",
            table: "referee_reports");

        migrationBuilder.DropIndex(
            name: "IX_referee_reports_reviewed_by_admin_id",
            table: "referee_reports");

        migrationBuilder.DropIndex(
            name: "IX_referee_reports_status",
            table: "referee_reports");

        migrationBuilder.DropColumn(
            name: "resubmitted_at",
            table: "referee_reports");

        migrationBuilder.DropColumn(
            name: "revision_number",
            table: "referee_reports");

        migrationBuilder.DropColumn(
            name: "reviewed_at",
            table: "referee_reports");

        migrationBuilder.DropColumn(
            name: "reviewed_by_admin_id",
            table: "referee_reports");

        migrationBuilder.DropColumn(
            name: "return_reason",
            table: "referee_reports");

        migrationBuilder.DropColumn(
            name: "return_reason_category",
            table: "referee_reports");

        migrationBuilder.DropColumn(
            name: "status",
            table: "referee_reports");

        migrationBuilder.DropColumn(
            name: "updated_at",
            table: "referee_reports");
    }
}
