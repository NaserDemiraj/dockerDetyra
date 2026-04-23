using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CodeLabAPI.Data;
using CodeLabAPI.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"] ?? "your-secret-key-min-32-characters");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(secretKey),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    });

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICodeExecutionService, CodeExecutionService>();
builder.Services.AddScoped<IDockerService, DockerService>();

var app = builder.Build();

// Create database and run migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    try
    {
        db.Database.EnsureCreated();
        logger.LogInformation("Database initialized successfully.");
        
        // Seed default test user if none exists
        if (!db.Users.Any())
        {
            var registerRequest = new RegisterRequest 
            { 
                Username = "testuser", 
                Email = "test@codelab.com", 
                Password = "Test@123456" 
            };
            var result = authService.RegisterAsync(registerRequest).Result;
            if (result.Success)
            {
                logger.LogInformation("Default test user created successfully.");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Failed to initialize the database. The application cannot start.");
        throw;
    }
}

app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
