using BackendApp.Data;
using BackendApp.Services;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;

// Load .env file if it exists (local dev), Railway sets env vars directly
try { Env.Load(); } catch { /* .env file not found — fine on Railway */ }

// Helper: get from DotNetEnv first, fall back to OS environment variable
static string GetEnv(string key) =>
    Env.GetString(key, fallback: null) ?? Environment.GetEnvironmentVariable(key) ?? "";

var builder = WebApplication.CreateBuilder(args);

// Railway provides PORT env var — bind to it
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Build MariaDB connection string from environment
var dbHost = GetEnv("DB_HOST");
var dbPort = GetEnv("DB_PORT");
var dbName = GetEnv("DB_NAME");
var dbUser = GetEnv("DB_USER");
var dbPassword = GetEnv("DB_PASSWORD");
var connectionString = $"Server={dbHost};Port={dbPort};Database={dbName};User={dbUser};Password={dbPassword};";

Console.WriteLine($"[Startup] DB_HOST={dbHost}, DB_PORT={dbPort}, DB_NAME={dbName}, DB_USER={dbUser}");
Console.WriteLine($"[Startup] Connection string configured (password hidden)");

// Register EF Core with MariaDB (Pomelo)
// Use a fixed server version to avoid AutoDetect connecting to the DB at config time
var serverVersion = new MariaDbServerVersion(new Version(10, 11, 0));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// Register the HttpClient and the ExternalApiService
builder.Services.AddHttpClient<IExternalApiService, ExternalApiService>();
builder.Services.AddScoped<IConversationService, ConversationService>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

var app = builder.Build();

// Auto-create database tables on startup
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Console.WriteLine("[Startup] Running EnsureCreated...");
        db.Database.EnsureCreated();
        Console.WriteLine("[Startup] Database ready.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Startup] Database init failed: {ex.GetType().Name}: {ex.Message}");
    // Don't crash — the app can still start and retry DB access on first request
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

// Health check endpoint so Railway can verify the app is alive
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

Console.WriteLine($"[Startup] App starting on port {port}...");
app.Run();
