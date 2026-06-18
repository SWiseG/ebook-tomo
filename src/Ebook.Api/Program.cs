using System.Text;
using Ebook.Api.Endpoints;
using Ebook.Api.OpenApi;
using Ebook.Api.Realtime;
using Ebook.Application;
using Ebook.Application.Common.Realtime;
using Ebook.Infrastructure;
using Ebook.Infrastructure.Observability;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Publishing;
using Ebook.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

// Utilitário operacional: gera hash PBKDF2 para AdminAuth (setup de dev e produção)
if (args is ["hash-password", var password])
{
    Console.WriteLine(new Pbkdf2PasswordHasher().Hash(password));
    return;
}

// Login interativo na Kiwify: salva a sessão (storageState) para a automação Playwright (E07)
if (args is ["kiwify-login"])
{
    var cfg = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .AddEnvironmentVariables()
        .Build();
    var kiwify = cfg.GetSection(KiwifyOptions.SectionName).Get<KiwifyOptions>() ?? new KiwifyOptions();
    await KiwifyLogin.RunAsync(kiwify.BaseUrl, kiwify.StorageStatePath);
    return;
}

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
    if (string.IsNullOrWhiteSpace(jwt.Secret))
    {
        throw new InvalidOperationException(
            "Jwt:Secret não configurado. Defina via appsettings ou variável de ambiente Jwt__Secret.");
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            // SignalR (WebSocket) não envia header Authorization no handshake:
            // aceita o JWT via query string "access_token" apenas no path do hub.
            o.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization();

    // Tempo real: SignalR + implementação que sobrescreve o NullRealtimeNotifier da Infrastructure.
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
        .AddCheck<DiskSpaceHealthCheck>("disk", tags: ["ready"])
        .AddCheck<DeadJobsHealthCheck>("dead-jobs", tags: ["ready"]);

    builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

    var app = builder.Build();

    // fontes embarcadas (docs/11): registra no QuestPDF + Skia antes de qualquer renderização
    Ebook.Infrastructure.Content.FontRegistry.Initialize(
        Path.Combine(AppContext.BaseDirectory, "assets", "fonts"));

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<EbookDbContext>();
        db.Database.Migrate();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;");
    }

    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapApiEndpoints();
    app.MapPublicEndpoints();
    app.MapHub<TomoHub>("/hubs/tomo");

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi(); // /openapi/v1.json
        app.MapScalarApiReference(o => o.WithTitle("EBOOK API")); // /scalar/v1
    }

    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

    // SPA Angular (copiado para wwwroot no build Docker; ausente em dev backend puro)
    if (File.Exists(Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html")))
    {
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapFallbackToFile("index.html");
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Falha fatal na inicialização");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposto para testes de integração (WebApplicationFactory).</summary>
public partial class Program;
