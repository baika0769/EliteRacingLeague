using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eliteracingleague.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationMetadataColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.notifications', 'related_type') IS NULL
BEGIN
    ALTER TABLE dbo.notifications ADD related_type NVARCHAR(50) NULL;
END;

IF COL_LENGTH('dbo.notifications', 'related_id') IS NULL
BEGIN
    ALTER TABLE dbo.notifications ADD related_id INT NULL;
END;

IF COL_LENGTH('dbo.notifications', 'action_type') IS NULL
BEGIN
    ALTER TABLE dbo.notifications ADD action_type NVARCHAR(50) NULL;
END;

IF COL_LENGTH('dbo.notifications', 'action_url') IS NULL
BEGIN
    ALTER TABLE dbo.notifications ADD action_url NVARCHAR(300) NULL;
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.notifications', 'action_url') IS NOT NULL
BEGIN
    ALTER TABLE dbo.notifications DROP COLUMN action_url;
END;

IF COL_LENGTH('dbo.notifications', 'action_type') IS NOT NULL
BEGIN
    ALTER TABLE dbo.notifications DROP COLUMN action_type;
END;

IF COL_LENGTH('dbo.notifications', 'related_id') IS NOT NULL
BEGIN
    ALTER TABLE dbo.notifications DROP COLUMN related_id;
END;

IF COL_LENGTH('dbo.notifications', 'related_type') IS NOT NULL
BEGIN
    ALTER TABLE dbo.notifications DROP COLUMN related_type;
END;
");
        }
    }
}
