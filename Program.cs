using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using KidZen.API.Data;
using KidZen.API.Hubs;
using KidZen.API.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Controllers & OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 2. Add DbContext (Default SQLite for easy self-contained zero-cost hosting)
var connectionString = builder.Configuration.GetConnectionString("KidZenDb") ?? "Data Source=kidzen.db";
var forceInMemory = builder.Configuration.GetValue<bool>("UseInMemoryDatabase", false);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (forceInMemory)
    {
        options.UseInMemoryDatabase("KidZenDb");
    }
    else if (connectionString.Contains(".db") || connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else if (connectionString.Contains("Server=") || connectionString.Contains("Trusted_Connection="))
    {
        options.UseSqlServer(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// 3. Register Custom Services
builder.Services.AddScoped<TokenService>();

// 4. Configure SignalR for real-time mobile push
builder.Services.AddSignalR();

// 5. Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "KidZenUltraSecureSecretKeyForSigningJWTTokens2026";
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "KidZen.API",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "KidZen.Client",
        ClockSkew = TimeSpan.Zero
    };

    // Support JWT tokens sent over WebSocket query string for SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/device"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// 6. Configure CORS for React Native & Web
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

var app = builder.Build();

// Ensure DB is initialized
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        if (db.Database.IsRelational())
        {
            db.Database.EnsureCreated();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("Relational DB initialization failed, falling back: {Message}", ex.Message);
    }
}

// HTTP Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<DeviceHub>("/hubs/device");

app.MapGet("/", () => Results.Json(new
{
    status = "Active",
    message = "KidZen Backend API is running successfully!",
    version = "1.0.0",
    database = "SQLite (kidzen.db)",
    realtimeHub = "/hubs/device",
    timeUtc = DateTime.UtcNow
}));

app.Run();
