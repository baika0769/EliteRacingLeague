# Security setup

Do not store production secrets in `appsettings.json`.

Use environment variables or `dotnet user-secrets` for local development:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<sql-connection-string>"
dotnet user-secrets set "Jwt:Key" "<at-least-32-characters-secret>"
dotnet user-secrets set "Jwt:Issuer" "EliteRacingLeague"
dotnet user-secrets set "Jwt:Audience" "EliteRacingLeagueUser"
dotnet user-secrets set "Jwt:ExpireMinutes" "30"
dotnet user-secrets set "Database:AutoMigrateOnStartup" "true"
dotnet user-secrets set "Smtp:Host" "<smtp-host>"
dotnet user-secrets set "Smtp:UserName" "<smtp-username>"
dotnet user-secrets set "Smtp:Password" "<smtp-password>"
dotnet user-secrets set "Smtp:FromEmail" "<from-email>"
```

Equivalent environment variable names:

```text
ConnectionStrings__DefaultConnection
Jwt__Key
Jwt__Issuer
Jwt__Audience
Jwt__ExpireMinutes
Database__AutoMigrateOnStartup
Smtp__Host
Smtp__UserName
Smtp__Password
Smtp__FromEmail
```

Rotate any SQL password, JWT key, and SMTP credentials that were previously committed to source.
