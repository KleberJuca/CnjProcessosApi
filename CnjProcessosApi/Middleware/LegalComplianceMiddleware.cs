using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CnjProcessosApi.Middleware;

public class LegalComplianceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;
    private readonly ILogger<LegalComplianceMiddleware> _logger;

    public LegalComplianceMiddleware(RequestDelegate next, IConfiguration config, ILogger<LegalComplianceMiddleware> logger)
    {
        _next = next;
        _config = config;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var nonCommercial = _config.GetValue<bool>("Legal:NonCommercialOnly");
        var termsUrl = _config["Legal:TermsOfUseUrl"] ?? string.Empty;
        var blockNonCommercial = Environment.GetEnvironmentVariable("BLOCK_NONCOMMERCIAL")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        // Always send advisory headers
        if (nonCommercial)
        {
            context.Response.Headers["X-DataJud-Terms"] = string.IsNullOrEmpty(termsUrl) ? "" : termsUrl;
            context.Response.Headers["X-NonCommercial"] = "true";
        }

        if (nonCommercial && blockNonCommercial && context.Request.Path.StartsWithSegments("/api"))
        {
            _logger.LogWarning("Request blocked due to non-commercial restriction. Path: {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status451UnavailableForLegalReasons;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Uso não permitido por razões legais",
                code = 451,
                details = "A API está configurada para uso não comercial e foi bloqueada neste ambiente.",
                termsOfUse = termsUrl
            });
            return;
        }

        await _next(context);
    }
}

public static class LegalComplianceMiddlewareExtensions
{
    public static IApplicationBuilder UseLegalCompliance(this IApplicationBuilder app)
    {
        return app.UseMiddleware<LegalComplianceMiddleware>();
    }
}
