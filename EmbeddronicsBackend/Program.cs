using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using EmbeddronicsBackend.Services;
using EmbeddronicsBackend.Models;
using EmbeddronicsBackend.Middleware;
using Microsoft.EntityFrameworkCore;
using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Models.Configuration;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/embeddronics-log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30
    )
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();


var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// Configure JWT settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();

// Add Entity Framework
builder.Services.AddDbContext<EmbeddronicsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Authentication Services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Add User Registration Service
builder.Services.AddScoped<IUserRegistrationService, UserRegistrationService>();

// Register services as Singletons to maintain the dictionary in memory
builder.Services.AddSingleton<IDataService<Product>, ProductService>();
builder.Services.AddSingleton<IDataService<Service>, ServiceService>();
builder.Services.AddSingleton<IDataService<Project>, ProjectService>();
builder.Services.AddSingleton<IDataService<BlogPost>, BlogService>();
builder.Services.AddSingleton<IDataService<Order>, OrderService>();
builder.Services.AddSingleton<IDataService<Client>, ClientService>();
builder.Services.AddSingleton<IDataService<Lead>, LeadService>();
builder.Services.AddSingleton<IDataService<Review>, ReviewService>();
builder.Services.AddSingleton<IDataService<FinancialRecord>, FinancialService>();
builder.Services.AddSingleton<IDataService<Quote>, QuoteService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy => policy
            .WithOrigins("http://localhost:5173", "http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

// Add JWT authentication with proper configuration
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false; // Set to true in production
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings?.Issuer,
        ValidAudience = jwtSettings?.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? "")),
        ClockSkew = TimeSpan.Zero // Remove default 5-minute tolerance
    };

    // Handle JWT events
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Log.Warning("JWT Authentication failed: {Error}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Log.Information("JWT Token validated for user: {User}", 
                context.Principal?.Identity?.Name);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Log.Warning("JWT Challenge triggered: {Error}", context.Error);
            return Task.CompletedTask;
        }
    };
});

// Add Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    options.AddPolicy("ClientOnly", policy => policy.RequireRole("client"));
    options.AddPolicy("AdminOrClient", policy => policy.RequireRole("admin", "client"));
});

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Embeddronics API",
        Version = "v1",
        Description = "API for Embeddronics application - PCB Design, Manufacturing & Electronics Services",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Embeddronics",
            Email = "contact@embeddronics.com"
        }
    });

    // Add JWT Authentication
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.\n\nExample: \"Bearer eyJhbGciOiJIUzI1NiIs...\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed the database with admin users
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();
    await DatabaseSeeder.SeedAsync(context);
}

// Configure the HTTP request pipeline.

// Add global exception handling middleware first
app.UseExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Embeddronics API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowReactApp");

// Add custom authentication middleware for logging and monitoring
app.UseCustomAuthentication();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
