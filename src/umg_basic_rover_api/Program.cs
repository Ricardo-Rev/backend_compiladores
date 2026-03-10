using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using umg_basic_rover_application.Contracts;
using umg_basic_rover_infrastructure.Services;
using umg_basic_rover_infrastructure.persistence.context;

var builder = WebApplication.CreateBuilder(args);

// 1. BASE DE DATOS
var connection_string = builder.Configuration.GetConnectionString("default_connection")
    ?? throw new InvalidOperationException("La cadena de conexión 'default_connection' no está configurada.");

builder.Services.AddDbContext<rover_db_context>(options =>
{
    options.UseSqlServer(connection_string, sql_options =>
    {
        sql_options.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        sql_options.CommandTimeout(30);
    });
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// 2. AUTENTICACIÓN JWT
var jwt_key = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key no está configurada.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime         = true,
        ValidIssuer              = builder.Configuration["Jwt:Issuer"],
        ValidAudience            = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt_key)),
        ClockSkew                = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 3. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend_policy", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000", "http://localhost:4200", "http://localhost:5173", "http://localhost:8080")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// 4. INYECCIÓN DE DEPENDENCIAS
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRecaptchaService, RecaptchaService>();
builder.Services.AddHttpClient("recaptcha");
builder.Services.AddControllers();

// 5. SWAGGER
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "UMG Rover Auth API",
        Version     = "v1",
        Description = "API de autenticación con reCAPTCHA v2 y JWT para el sistema UMG Rover."
    });

    var jwt_scheme = new OpenApiSecurityScheme
    {
        Scheme       = "bearer",
        BearerFormat = "JWT",
        Name         = "Authorization",
        In           = ParameterLocation.Header,
        Type         = SecuritySchemeType.Http,
        Description  = "Ingresa tu token JWT aquí (sin el prefijo 'Bearer').",
        Reference    = new OpenApiReference
        {
            Id   = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };

    c.AddSecurityDefinition(jwt_scheme.Reference.Id, jwt_scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwt_scheme, Array.Empty<string>() }
    });
});

var app = builder.Build();

// 6. MIDDLEWARE PIPELINE
app.UseExceptionHandler(error_app =>
{
    error_app.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var error  = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        logger.LogError(error, "[GLOBAL-ERROR] Path={Path}", context.Request.Path);
        context.Response.StatusCode  = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Error interno del servidor." });
    });
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "UMG Rover Auth API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseCors("frontend_policy");
app.UseAuthentication();

// Middleware de revocación de tokens
app.Use(async (context, next) =>
{
    var auth_header = context.Request.Headers.Authorization.ToString();
    if (!string.IsNullOrWhiteSpace(auth_header) &&
        auth_header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        var token = auth_header[7..].Trim();
        var jwt   = context.RequestServices.GetRequiredService<IJwtTokenService>();
        var db    = context.RequestServices.GetRequiredService<rover_db_context>();
        var hash  = jwt.ComputeSha256(token);

        var sesion_activa = await db.sesiones
            .AsNoTracking()
            .AnyAsync(s => s.session_token == hash && s.activa);

        if (!sesion_activa)
        {
            context.Response.StatusCode  = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Sesión no activa o token revocado." });
            return;
        }
    }
    await next();
});

app.UseAuthorization();
app.MapControllers();

// Health checks
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow })).AllowAnonymous();
app.MapGet("/health/database", async (rover_db_context context) =>
{
    try
    {
        var can_connect = await context.Database.CanConnectAsync();
        if (!can_connect) return Results.Problem("No se pudo conectar a la base de datos.");
        return Results.Ok(new { status = "database connected", timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error de base de datos: {ex.Message}");
    }
}).AllowAnonymous();

app.Run();