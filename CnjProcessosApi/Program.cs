using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;
using CnjProcessosApi.Infrastructure.Persistence;
using CnjProcessosApi.Infrastructure.Http;
using CnjProcessosApi.Core.Interfaces;
using CnjProcessosApi.Infrastructure.Repositories;
using CnjProcessosApi.Core.Services;
using CnjProcessosApi.Infrastructure.Jobs;
using Polly.Extensions.Http;
using System.Net;
using CnjProcessosApi.Middleware;
using Polly; // add polly root namespace

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

// Controllers + Razor Pages (frontend simples)
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// EF Core SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<DataJudRequestLimiter>();
builder.Services.AddTransient<DataJudRateLimitHandler>();
// HttpClient + Polly for CNJ
builder.Services.AddHttpClient<ICnjClient, CnjClient>(client =>
{
    var baseUrl = builder.Configuration["DataJud:BaseUrl"] ?? "https://api-publica.datajud.cnj.jus.br/";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    var apiKey = builder.Configuration["DataJud:ApiKey"] ?? string.Empty;
    client.Timeout = TimeSpan.FromSeconds(45);
    if (!string.IsNullOrWhiteSpace(apiKey) && !apiKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("APIKey", apiKey.Trim());
})
.RedactLoggedHeaders(new[] { "Authorization" })
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
.AddHttpMessageHandler<DataJudRateLimitHandler>();

// Repositories & Services
builder.Services.AddScoped<IProcessoRepository, ProcessoRepository>();
builder.Services.AddScoped<IProcessoService, ProcessoService>();

// Quartz (diário)
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("ProcessoSyncJob");
    q.AddJob<ProcessoSyncJob>(opts => opts.WithIdentity(jobKey));

    var cron = builder.Configuration["Quartz:SyncIntervalCron"] ?? "0 0 0 * * ?";
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("ProcessoSyncJob-trigger")
        .WithCronSchedule(cron));
});
builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

// Rate Limiting por IP (120/min)
builder.Services.AddRateLimiter(options =>
{
    var permitLimit = Math.Clamp(builder.Configuration.GetValue<int>("RateLimiting:PermitLimitPerMinute", 120), 1, 120);
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Limite de requisições atingido. Aguarde um minuto." }, ct);
    };
    options.AddPolicy("per-ip", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "CnjProcessos API Pública", Version = "v1" });
});

// HealthChecks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandler();
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var terms = builder.Configuration["Legal:TermsOfUseUrl"] ?? "";
        options.DocumentTitle = "CnjProcessos API Pública (Uso não comercial)";
    });
}

app.UseSerilogRequestLogging();
if (builder.Configuration.GetValue<bool>("HttpsRedirection:Enabled"))
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseLegalCompliance();
app.UseRateLimiter();

app.MapHealthChecks("/health").RequireRateLimiting("per-ip");
app.MapControllers().RequireRateLimiting("per-ip");
app.MapRazorPages().RequireRateLimiting("per-ip");

app.Run();

public partial class Program { }
