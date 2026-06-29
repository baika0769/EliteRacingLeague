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

// JWT Authentication configuration
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtKey = builder.Configuration["Jwt:Key"];

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey!)
        )
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

if (app.Configuration.GetValue<bool>("Database:AutoMigrateOnStartup"))
{
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

app.UseCors("AllowFrontend");

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();



app.Run();
