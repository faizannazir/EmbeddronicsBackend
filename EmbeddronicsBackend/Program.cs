using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using EmbeddronicsBackend.Services;
using EmbeddronicsBackend.Models;
using EmbeddronicsBackend.Middleware;
using Microsoft.EntityFrameworkCore;
using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Data.Repositories;
using EmbeddronicsBackend.Models.Configuration;
using FluentValidation;
using FluentValidation.AspNetCore;
using EmbeddronicsBackend.Validators;
using EmbeddronicsBackend.Filters;

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
builder.Services.AddScoped<ITokenBlacklistService, TokenBlacklistService>();

// Add memory cache for token blacklisting
builder.Services.AddMemoryCache();

// Add User Registration Service
builder.Services.AddScoped<IUserRegistrationService, UserRegistrationService>();

// Add Repository Pattern and Unit of Work
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IQuoteRepository, QuoteRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();

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
    // Role-based policies
    options.AddPolicy("AdminOnly", policy => 
        policy.RequireRole("admin")
              .RequireAuthenticatedUser());
    
    options.AddPolicy("ClientOnly", policy => 
        policy.RequireRole("client")
              .RequireAuthenticatedUser());
    
    options.AddPolicy("AdminOrClient", policy => 
        policy.RequireRole("admin", "client")
              .RequireAuthenticatedUser());
    
    // Admin CRM policies - comprehensive access for admin operations
    options.AddPolicy("AdminCRM", policy =>
        policy.RequireRole("admin")
              .RequireAuthenticatedUser());
    
    // Client portal policies - restricted access for client operations
    options.AddPolicy("ClientPortal", policy =>
        policy.RequireRole("client")
              .RequireAuthenticatedUser());
    
    // Resource management policies
    options.AddPolicy("ManageProducts", policy =>
        policy.RequireRole("admin")
              .RequireAuthenticatedUser());
    
    options.AddPolicy("ManageOrders", policy =>
        policy.RequireRole("admin")
              .RequireAuthenticatedUser());
    
    options.AddPolicy("ManageQuotes", policy =>
        policy.RequireRole("admin")
              .RequireAuthenticatedUser());
    
    options.AddPolicy("ManageClients", policy =>
        policy.RequireRole("admin")
              .RequireAuthenticatedUser());
    
    // Resource-based authorization policies for ownership
    options.AddPolicy("OrderOwnership", policy => 
    {
        policy.Requirements.Add(new EmbeddronicsBackend.Authorization.Requirements.ResourceOwnershipRequirement("Order"));
        policy.RequireAuthenticatedUser();
    });
    
    options.AddPolicy("QuoteOwnership", policy => 
    {
        policy.Requirements.Add(new EmbeddronicsBackend.Authorization.Requirements.ResourceOwnershipRequirement("Quote"));
        policy.RequireAuthenticatedUser();
    });
    
    options.AddPolicy("MessageOwnership", policy => 
    {
        policy.Requirements.Add(new EmbeddronicsBackend.Authorization.Requirements.ResourceOwnershipRequirement("Message"));
        policy.RequireAuthenticatedUser();
    });
    
    // Public access policy for anonymous endpoints
    options.AddPolicy("PublicAccess", policy =>
        policy.RequireAssertion(context => true)); // Always allow
    
    // Default fallback policy - require authentication
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Register authorization handlers
builder.Services.AddScoped<IAuthorizationHandler, EmbeddronicsBackend.Authorization.Handlers.ResourceOwnershipHandler>();
builder.Services.AddScoped<IAuthorizationHandler, EmbeddronicsBackend.Authorization.Handlers.AdminOperationHandler>();

// Add services to the container.

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

// Configure FluentValidation to use camelCase property names for error messages
ValidatorOptions.Global.PropertyNameResolver = (type, memberInfo, expression) =>
{
    return memberInfo?.Name?.Substring(0, 1).ToLower() + memberInfo?.Name?.Substring(1);
};

builder.Services.AddControllers(options =>
{
    // Add global validation filter
    options.Filters.Add<ValidationFilter>();
});
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
