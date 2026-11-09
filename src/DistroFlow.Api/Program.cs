using DistroFlow.Api.Extensions;
using DistroFlow.Application;
using DistroFlow.Infrastructure;
using DistroFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---- Services (what the app is made of) ----
builder.Services.AddOpenApi();
builder.Services.AddApiControllers();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication();
builder.Services.AddCurrentTenant();

var app = builder.Build();

// ---- Request pipeline (what happens to each request, in order) ----
app.MapGet("/health/database", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();

    return canConnect
        ? Results.Ok(new
        {
            status = "Connected",
            database = "distroflow_dev"
        })
        : Results.Problem("Unable to connect to PostgreSQL.");
})
.AllowAnonymous(); // probed by Docker/monitoring, which have no token

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Lets the integration tests reference Program in WebApplicationFactory<Program>.
public partial class Program { }
