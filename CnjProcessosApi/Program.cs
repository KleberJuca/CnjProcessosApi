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

// Listen on localhost and all interfaces for HTTP (5000) and HTTPS (5001)
builder.WebHost.UseUrls("http://localhost:5000;http://0.0.0.0:5000;https://localhost:5001;https://0.0.0.0:5001");

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

// Controllers + Razor Pages (frontend simples)
builder.Services.AddControllers();
builder.Services.AddRazorPages();

// EF Core SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// HttpClient + Polly for CNJ
builder.Services.AddHttpClient<ICnjClient, CnjClient>(client =>
{
    var baseUrl = builder.Configuration["DataJud:BaseUrl"] ?? "";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    var apiKey = builder.Configuration["DataJud:ApiKey"] ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Add("Authorization", $"APIKey {apiKey}");
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

// Repositories & Services
builder.Services.AddScoped<IProcessoRepository, ProcessoRepository>();
builder.Services.AddScoped<IProcessoService, ProcessoService>();

// Quartz (diário)
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();

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
var permitLimit = builder.Configuration.GetValue<int>("RateLimiting:PermitLimitPerMinute", 120);
builder.Services.AddRateLimiter(_ =>
    _.AddPolicy("per-ip", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    })
);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "CnjProcessos API Pública", Version = "v1" });
});

// HealthChecks
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var terms = builder.Configuration["Legal:TermsOfUseUrl"] ?? "";
        options.DocumentTitle = "CnjProcessos API Pública (Uso não comercial)";
    });
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseLegalCompliance();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapControllers().RequireRateLimiting("per-ip");
app.MapRazorPages().RequireRateLimiting("per-ip");

app.Run();
