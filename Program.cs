
using Eliteracingleague.API.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddDbContext<EliteRacingLeagueContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/db-test", async (EliteRacingLeagueContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();

        if (!canConnect)
        {
            return Results.BadRequest("Không kết nối được SQL Server.");
        }

        var connection = db.Database.GetDbConnection();

        return Results.Ok(new
        {
            Message = "Kết nối SQL thành công",
            Server = connection.DataSource,
            Database = connection.Database
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Lỗi kết nối SQL Server",
            detail: ex.Message
        );
    }
});

app.MapGet("/breeds", async (EliteRacingLeagueContext db) =>
{
    var breeds = await db.HorseBreeds.ToListAsync();
    return Results.Ok(breeds);
});

app.Run();