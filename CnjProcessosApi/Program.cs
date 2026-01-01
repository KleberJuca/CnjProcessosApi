using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Quartz;
using Serilog;
using CnjProcessosApi.Infrastructure.Persistence;
using CnjProcessosApi.Infrastructure.Http;
using CnjProcessosApi.Core.Interfaces;
using CnjProcessosApi.Infrastructure.Repositories;
using CnjProcessosApi.Core.Services;
using CnjProcessosApi.Infrastructure.Jobs;
using Polly.Extensions.Http;
using Polly;
using System.Net;
using Microsoft.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

// Controllers
builder.Services.AddControllers();

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
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

// Repositories & Services
builder.Services.AddScoped<IProcessoRepository, ProcessoRepository>();
builder.Services.AddScoped<IProcessoService, ProcessoService>();

// Quartz
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();

    var jobKey = new JobKey("ProcessoSyncJob");
    q.AddJob<ProcessoSyncJob>(opts => opts.WithIdentity(jobKey));

    var cron = builder.Configuration["Quartz:SyncIntervalCron"] ?? "0 0 */4 * * ?";
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("ProcessoSyncJob-trigger")
        .WithCronSchedule(cron));
});
builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

// Authentication JWT
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "";
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = key
    };
});

// Authorization
builder.Services.AddAuthorization();

// Swagger + JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "CnjProcessos API", Version = "v1" });
    var securityScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header"
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    var securityReq = new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        { securityScheme, new List<string>() }
    };
    c.AddSecurityRequirement(securityReq);
});

// HealthChecks
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
