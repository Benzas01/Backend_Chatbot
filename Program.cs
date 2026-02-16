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

// Register EF Core with MariaDB (Pomelo)
var serverVersion = ServerVersion.AutoDetect(connectionString);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// Register the HttpClient and the ExternalApiService
builder.Services.AddHttpClient<IExternalApiService, ExternalApiService>();
builder.Services.AddScoped<IConversationService, ConversationService>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

var app = builder.Build();

// Auto-create database tables on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.UseCors("AllowAll");

app.MapControllers();

app.Run();
