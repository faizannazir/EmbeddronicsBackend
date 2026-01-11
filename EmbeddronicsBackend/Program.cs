using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using Serilog.Events;
using EmbeddronicsBackend.Services;
using EmbeddronicsBackend.Services.Monitoring;
using EmbeddronicsBackend.Services.HealthChecks;
using EmbeddronicsBackend.Services.Caching;
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
using EmbeddronicsBackend.Hubs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.Server.Kestrel.Core;

// Configure Serilog with structured logging and enrichment
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "EmbeddronicsAPI")
    .Enrich.WithProperty("MachineName", Environment.MachineName)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/embeddronics-log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"
    )
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();


var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// Configure Performance Settings
var performanceSettings = builder.Configuration.GetSection("Performance").Get<PerformanceSettings>() ?? new PerformanceSettings();

// Configure Kestrel server limits for request size and timeouts
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = performanceSettings.RequestLimits.MaxRequestBodySize;
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(performanceSettings.RequestLimits.RequestTimeoutSeconds);
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    serverOptions.Limits.MaxRequestLineSize = performanceSettings.RequestLimits.MaxUrlLength;
    serverOptions.Limits.MaxRequestHeadersTotalSize = 32768; // 32KB
});

// Configure JWT settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();

// Configure Performance Settings
builder.Services.Configure<PerformanceSettings>(builder.Configuration.GetSection("Performance"));

// Add Distributed Caching
if (performanceSettings.Cache.UseRedis)
{
    // Use Redis for distributed caching
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = performanceSettings.Cache.RedisConnectionString;
        options.InstanceName = performanceSettings.Cache.RedisInstanceName;
    });
    builder.Services.AddSingleton<ICacheService, DistributedCacheService>();
    Log.Information("Redis distributed caching enabled: {ConnectionString}", performanceSettings.Cache.RedisConnectionString);
}
else
{
    // Use in-memory distributed cache as fallback
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
    Log.Information("In-memory caching enabled (Redis disabled)");
}

// Add Response Caching
builder.Services.AddResponseCaching(options =>
{
    options.MaximumBodySize = 64 * 1024 * 1024; // 64MB max cached response
    options.SizeLimit = 256 * 1024 * 1024; // 256MB total cache size
    options.UseCaseSensitivePaths = false;
});

// Add Entity Framework
builder.Services.AddDbContext<EmbeddronicsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Authentication Services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenBlacklistService, TokenBlacklistService>();

// Add Email Service
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

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

// Add Business Services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IQuoteService, QuoteService>();
builder.Services.AddScoped<IQuoteWorkflowService, QuoteWorkflowService>();

// Add Background Services
builder.Services.AddHostedService<QuoteExpirationService>();

// Add Performance Monitoring
builder.Services.AddSingleton<IPerformanceMonitorService, PerformanceMonitorService>();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready", "db" })
    .AddCheck<MemoryHealthCheck>("memory", tags: new[] { "live" })
    .AddCheck<SignalRHealthCheck>("signalr", tags: new[] { "ready" })
    .AddCheck<ExternalServicesHealthCheck>("external-services", tags: new[] { "ready" })
    .AddCheck<DiskSpaceHealthCheck>("disk-space", tags: new[] { "live" });

// Add SignalR for real-time chat
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 102400; // 100KB max message size
});

// Add Chat Services
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddSingleton<IConnectionManagerService, ConnectionManagerService>();
builder.Services.AddScoped<IChatAttachmentService, ChatAttachmentService>();
builder.Services.AddScoped<IUserStatusService, UserStatusService>();

// Add Chat Security Services
builder.Services.AddSingleton<IChatRateLimitService, ChatRateLimitService>();
builder.Services.AddSingleton<IChatAuthorizationService, ChatAuthorizationService>();
builder.Services.AddSingleton<ISessionManagementService, SessionManagementService>();

// Register legacy JSON services as Singletons to maintain the dictionary in memory
builder.Services.AddSingleton<IDataService<Product>, ProductService>();
builder.Services.AddSingleton<IDataService<Service>, ServiceService>();
builder.Services.AddSingleton<IDataService<Project>, ProjectService>();
builder.Services.AddSingleton<IDataService<BlogPost>, BlogService>();
// Note: OrderService and QuoteService are now using the new interfaces instead of IDataService
builder.Services.AddSingleton<IDataService<Client>, ClientService>();
builder.Services.AddSingleton<IDataService<Lead>, LeadService>();
builder.Services.AddSingleton<IDataService<Review>, ReviewService>();
builder.Services.AddSingleton<IDataService<FinancialRecord>, FinancialService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy => policy
            .WithOrigins("http://localhost:5173", "http://localhost:3000", "http://localhost:5001", "https://localhost:7001")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
    
    // Add a more permissive policy for development testing
    options.AddPolicy("AllowDevelopment",
        policy => policy
            .SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
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
        },
        // Handle SignalR token from query string
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            
            // If the request is for the chat hub, read the token from the query string
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
            {
                context.Token = accessToken;
            }
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
        Description = "JWT Authorization header using the Bearer scheme. Enter your token below (without 'Bearer ' prefix).\n\nExample: \"eyJhbGciOiJIUzI1NiIs...\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
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

// Add security headers early in the pipeline
app.UseSecurityHeaders(options =>
{
    options.EnableHsts = performanceSettings.Security.EnableHsts;
    options.HstsMaxAgeSeconds = performanceSettings.Security.HstsMaxAgeSeconds;
    options.EnableCrossOriginPolicies = performanceSettings.Security.EnableCrossOriginPolicies;
    options.XFrameOptions = performanceSettings.Security.XFrameOptions;
    if (!string.IsNullOrEmpty(performanceSettings.Security.ContentSecurityPolicy))
        options.ContentSecurityPolicy = performanceSettings.Security.ContentSecurityPolicy;
});

// Add correlation ID middleware for request tracking
app.UseCorrelationId();

// Add response start logging to capture the first writer and stack trace when the response begins
app.UseResponseStartLogging();

// Add rate limiting middleware
if (performanceSettings.RateLimit.Enabled)
{
    app.UseRateLimiting(options =>
    {
        options.Enabled = performanceSettings.RateLimit.Enabled;
        options.PermitLimit = performanceSettings.RateLimit.PermitLimit;
        options.WindowSeconds = performanceSettings.RateLimit.WindowSeconds;
        options.AuthenticatedPermitLimit = performanceSettings.RateLimit.AuthenticatedPermitLimit;
        options.AnonymousPermitLimit = performanceSettings.RateLimit.AnonymousPermitLimit;
        options.ExemptEndpoints = performanceSettings.RateLimit.ExemptEndpoints;
        
        // Configure endpoint-specific limits
        foreach (var limit in performanceSettings.RateLimit.EndpointLimits)
        {
            options.EndpointLimits[limit.Key] = new EndpointRateLimit
            {
                PermitLimit = limit.Value.PermitLimit,
                WindowSeconds = limit.Value.WindowSeconds
            };
        }
    });
    Log.Information("Rate limiting enabled");
}

// Add request metrics middleware for performance monitoring
app.UseRequestMetrics();

// Add global exception handling middleware
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

// Add response caching middleware
app.UseResponseCaching();

// Add custom response caching for fine-grained control
app.UseCustomResponseCaching();

if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowDevelopment");
}
else
{
    app.UseCors("AllowReactApp");
}

// Add custom authentication middleware for logging and monitoring
app.UseCustomAuthentication();

app.UseAuthentication();
app.UseAuthorization();

// Map health check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapControllers();

// Map SignalR hub for real-time chat
app.MapHub<ChatHub>("/hubs/chat");

Log.Information("Embeddronics API started successfully");

app.Run();
