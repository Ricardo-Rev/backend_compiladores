using Microsoft.EntityFrameworkCore;
using umg_basic_rover_infrastructure.persistence.context;

var builder = WebApplication.CreateBuilder(args);

// ================================
// DATABASE CONFIGURATION
// ================================

var connection_string = builder.Configuration.GetConnectionString("default_connection");

builder.Services.AddDbContext<rover_db_context>(options =>
{
    options.UseSqlServer(connection_string);
    options.EnableSensitiveDataLogging(false);
});

// ================================
// SERVICES
// ================================

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ================================
// BUILD APPLICATION
// ================================

var app = builder.Build();

// ================================
// MIDDLEWARE
// ================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// ================================
// DATABASE HEALTH CHECK ENDPOINT
// ================================

app.MapGet("/health/database", async (rover_db_context context) =>
{
    try
    {
        var can_connect = await context.Database.CanConnectAsync();

        if (!can_connect)
            return Results.Problem("Database connection failed");

        return Results.Ok("Database connected successfully");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Database error: {ex.Message}");
    }
});

app.Run();