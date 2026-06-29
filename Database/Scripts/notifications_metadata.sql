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
