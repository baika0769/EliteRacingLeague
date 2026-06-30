using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Eliteracingleague.API.Data;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Services;
using Eliteracingleague.API.Services.Email;
using Eliteracingleague.API.Services.JockeyMatching;
using Eliteracingleague.API.Services.Leaderboards;
using Eliteracingleague.API.Services.Notifications;
using Eliteracingleague.API.Services.SystemTime;
using Eliteracingleague.API.Constants;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5174",
                "https://localhost:5174",
                "http://localhost:5173",
                "https://localhost:5173",
                "http://localhost:3000",
                "https://localhost:3000"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


builder.Services.AddDbContext<EliteRacingLeagueContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<JockeyAccessService>();
builder.Services.AddScoped<IJockeyMatchScoreService, JockeyMatchScoreService>();
builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
builder.Services.AddScoped<IRaceTimeStatusService, RaceTimeStatusService>();
builder.Services.AddScoped<SpectatorLeaderboardService>();
builder.Services.AddScoped<PredictionEvaluationService>();
builder.Services.AddScoped<TournamentStatusService>();
builder.Services.AddScoped<RefereeRaceLifecycleService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();

if (builder.Configuration.GetValue("SystemTime:EnableBackgroundSync", false))
{
    builder.Services.AddHostedService<RaceTimeStatusBackgroundService>();
}

var configuredJwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(configuredJwtKey)
    || configuredJwtKey == "CHANGE_ME_USE_USER_SECRETS_OR_ENVIRONMENT"
    || configuredJwtKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key is missing, placeholder, or too short.");
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (!context.HttpContext.Response.HasStarted)
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { message = "Too many login attempts. Please try again later." },
                cancellationToken);
        }
    };

    options.AddPolicy("LoginRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// JWT Authentication configuration
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = ClaimTypes.NameIdentifier,
        RoleClaimType = ClaimTypes.Role,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuredJwtKey)
        )
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userIdText = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdText, out var userId))
            {
                context.Fail("Invalid token.");
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<EliteRacingLeagueContext>();
            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null || !user.EmailVerified)
            {
                context.Fail("Invalid token.");
                return;
            }

            if (user.Status == UserStatuses.Inactive || user.Status == UserStatuses.Banned)
            {
                context.Fail("Invalid token.");
                return;
            }

            var tokenRole = context.Principal?.FindFirst(ClaimTypes.Role)?.Value;
            if (!string.Equals(user.Role, tokenRole, StringComparison.Ordinal))
            {
                context.Fail("Invalid token.");
            }
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT token theo dạng: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

var autoMigrate = app.Configuration.GetValue<bool>("Database:AutoMigrateOnStartup");
if (autoMigrate)
{
    var connectionString = app.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Database:AutoMigrateOnStartup is enabled but ConnectionStrings:DefaultConnection is missing.");
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<EliteRacingLeagueContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}





app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowFrontend");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();



app.Run();
