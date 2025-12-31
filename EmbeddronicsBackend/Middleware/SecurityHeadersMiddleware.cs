namespace EmbeddronicsBackend.Middleware;

/// <summary>
/// Configuration options for security headers
/// </summary>
public class SecurityHeadersOptions
{
    /// <summary>
    /// Enable Strict-Transport-Security header (HSTS)
    /// </summary>
    public bool EnableHsts { get; set; } = true;

    /// <summary>
    /// HSTS max age in seconds (default: 1 year)
    /// </summary>
    public int HstsMaxAgeSeconds { get; set; } = 31536000;

    /// <summary>
    /// Include subdomains in HSTS
    /// </summary>
    public bool HstsIncludeSubdomains { get; set; } = true;

    /// <summary>
    /// Add HSTS preload directive
    /// </summary>
    public bool HstsPreload { get; set; } = false;

    /// <summary>
    /// Content Security Policy directives
    /// </summary>
    public string ContentSecurityPolicy { get; set; } = 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self' ws: wss: http://localhost:* https://localhost:*; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "base-uri 'self';";

    /// <summary>
    /// X-Frame-Options value
    /// </summary>
    public string XFrameOptions { get; set; } = "DENY";

    /// <summary>
    /// X-Content-Type-Options value
    /// </summary>
    public string XContentTypeOptions { get; set; } = "nosniff";

    /// <summary>
    /// Referrer-Policy value
    /// </summary>
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    /// Permissions-Policy (formerly Feature-Policy)
    /// </summary>
    public string PermissionsPolicy { get; set; } = 
        "accelerometer=(), " +
        "camera=(), " +
        "geolocation=(), " +
        "gyroscope=(), " +
        "magnetometer=(), " +
        "microphone=(), " +
        "payment=(), " +
        "usb=()";

    /// <summary>
    /// Cross-Origin-Embedder-Policy
    /// </summary>
    public string CrossOriginEmbedderPolicy { get; set; } = "require-corp";

    /// <summary>
    /// Cross-Origin-Opener-Policy
    /// </summary>
    public string CrossOriginOpenerPolicy { get; set; } = "same-origin";

    /// <summary>
    /// Cross-Origin-Resource-Policy
    /// </summary>
    public string CrossOriginResourcePolicy { get; set; } = "same-origin";

    /// <summary>
    /// Enable Cross-Origin policies (may break some functionality)
    /// </summary>
    public bool EnableCrossOriginPolicies { get; set; } = false;
}

/// <summary>
/// Middleware for adding security headers to HTTP responses
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;
    private readonly SecurityHeadersOptions _options;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ILogger<SecurityHeadersMiddleware> logger,
        SecurityHeadersOptions? options = null)
    {
        _next = next;
        _logger = logger;
        _options = options ?? new SecurityHeadersOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before the response is sent
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // X-Content-Type-Options - Prevent MIME sniffing
            if (!headers.ContainsKey("X-Content-Type-Options"))
                headers["X-Content-Type-Options"] = _options.XContentTypeOptions;

            // X-Frame-Options - Prevent clickjacking
            if (!headers.ContainsKey("X-Frame-Options"))
                headers["X-Frame-Options"] = _options.XFrameOptions;

            // X-XSS-Protection - Legacy XSS protection (deprecated but still useful for older browsers)
            if (!headers.ContainsKey("X-XSS-Protection"))
                headers["X-XSS-Protection"] = "1; mode=block";

            // Referrer-Policy - Control referrer information
            if (!headers.ContainsKey("Referrer-Policy"))
                headers["Referrer-Policy"] = _options.ReferrerPolicy;

            // Content-Security-Policy
            if (!headers.ContainsKey("Content-Security-Policy") && !string.IsNullOrEmpty(_options.ContentSecurityPolicy))
                headers["Content-Security-Policy"] = _options.ContentSecurityPolicy;

            // Permissions-Policy (formerly Feature-Policy)
            if (!headers.ContainsKey("Permissions-Policy") && !string.IsNullOrEmpty(_options.PermissionsPolicy))
                headers["Permissions-Policy"] = _options.PermissionsPolicy;

            // Strict-Transport-Security (HSTS) - Only for HTTPS
            if (_options.EnableHsts && context.Request.IsHttps)
            {
                var hstsValue = $"max-age={_options.HstsMaxAgeSeconds}";
                if (_options.HstsIncludeSubdomains)
                    hstsValue += "; includeSubDomains";
                if (_options.HstsPreload)
                    hstsValue += "; preload";

                if (!headers.ContainsKey("Strict-Transport-Security"))
                    headers["Strict-Transport-Security"] = hstsValue;
            }

            // Cross-Origin policies (optional, can break some functionality)
            if (_options.EnableCrossOriginPolicies)
            {
                if (!headers.ContainsKey("Cross-Origin-Embedder-Policy"))
                    headers["Cross-Origin-Embedder-Policy"] = _options.CrossOriginEmbedderPolicy;

                if (!headers.ContainsKey("Cross-Origin-Opener-Policy"))
                    headers["Cross-Origin-Opener-Policy"] = _options.CrossOriginOpenerPolicy;

                if (!headers.ContainsKey("Cross-Origin-Resource-Policy"))
                    headers["Cross-Origin-Resource-Policy"] = _options.CrossOriginResourcePolicy;
            }

            // Remove potentially sensitive headers
            headers.Remove("X-Powered-By");
            headers.Remove("Server");
            headers.Remove("X-AspNet-Version");
            headers.Remove("X-AspNetMvc-Version");

            return Task.CompletedTask;
        });

        await _next(context);
    }
}

/// <summary>
/// Extension methods for security headers middleware
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, SecurityHeadersOptions options)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>(options);
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, Action<SecurityHeadersOptions> configure)
    {
        var options = new SecurityHeadersOptions();
        configure(options);
        return app.UseMiddleware<SecurityHeadersMiddleware>(options);
    }
}
