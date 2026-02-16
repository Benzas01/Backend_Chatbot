using BackendApp.Data;
using BackendApp.Services;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;

// Top-level try-catch to prevent ANY unhandled exception from crashing
try
{
    // Load .env file if it exists (local dev), Railway sets env vars directly
    try { Env.Load(); } catch { /* .env file not found — fine on Railway */ }

    // Helper: get from DotNetEnv first, fall back to OS environment variable
    static string GetEnv(string key) =>
        Env.GetString(key, fallback: null) ?? Environment.GetEnvironmentVariable(key) ?? "";

    var builder = WebApplication.CreateBuilder(args);

    // Railway provides PORT env var — bind to it
    var portVar = Environment.GetEnvironmentVariable("PORT");
    var port = !string.IsNullOrEmpty(portVar) ? int.Parse(portVar) : 8080;
    
    Console.WriteLine($"[Startup] PORT environment variable: '{portVar}' -> Binding to port: {port}");

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(port);
    });

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
    Console.WriteLine("[Startup] Configuring EF Core...");
    var serverVersion = new MariaDbServerVersion(new Version(10, 11, 0));
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySql(connectionString, serverVersion));
    Console.WriteLine("[Startup] EF Core configured.");

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

    Console.WriteLine("[Startup] Building app...");
    var app = builder.Build();
    Console.WriteLine("[Startup] App built successfully.");

    // Auto-create database tables on startup
    try
    {
        Console.WriteLine("[Startup] Running EnsureCreated...");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            Console.WriteLine("[Startup] Database ready.");
        }
    }
    catch (Exception ex)
    {
        // Use a safe way to print the exception — avoid calling ToString() on MySQL exceptions
        try
        {
            Console.WriteLine($"[Startup] Database init failed: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[Startup] Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
        }
        catch
        {
            Console.WriteLine("[Startup] Database init failed (exception details unavailable)");
        }
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
}
catch (Exception ex)
{
    // Last resort — catch absolutely anything that crashes the app
    try
    {
        Console.WriteLine($"[FATAL] Application crashed: {ex.GetType().Name}: {ex.Message}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"[FATAL] Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        }
        Console.WriteLine($"[FATAL] StackTrace: {ex.StackTrace}");
    }
    catch
    {
        Console.WriteLine("[FATAL] Application crashed and exception details could not be printed.");
    }
}
