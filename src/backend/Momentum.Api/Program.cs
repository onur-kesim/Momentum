using Asp.Versioning;
using Asp.Versioning.Builder;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Momentum.Api.Auth;
using Momentum.Api.Endpoints;
using Momentum.Api.Health;
using Momentum.Api.Realtime;
using Momentum.Api.Web;
using Momentum.Application.Abstractions;
using Momentum.Application.Abstractions.Auth;
using Momentum.Application.Abstractions.Sync;
using Momentum.Application.Behaviors;
using Momentum.Application.Features.Auth;
using Momentum.Application.Features.Sync;
using Momentum.Infrastructure;
using Momentum.Infrastructure.Sync;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);

// slice-2b1 D1: the connection string comes from user-secrets/env (red line #1); no DB configured ->
// the host still boots DB-less (slice-1 contract) and the Npgsql readiness probe is not registered.
var connectionString = builder.Configuration.GetConnectionString("Momentum");
var hasDatabase = !string.IsNullOrWhiteSpace(connectionString);

// --- ADR 0001 K-G3: structured logging (Serilog) --------------------------------------------
builder.Host.UseSerilog((_, configuration) => configuration
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console());

// --- ADR 0001 K-C5: the clock is TimeProvider ONLY (DateTime.* is banned in production) -------
builder.Services.AddSingleton(TimeProvider.System);

// --- ADR 0001 K-B1/K-B1a: CQRS mediator. Lifetime = Scoped is set in Application via
//     [assembly: MediatorOptions] (compile-time; handlers may depend on scoped persistence ports). ----
builder.Services.AddMediator();

// --- slice-2b1: persistence (SyncDbContext + raw-SQL ports) + sync request pipeline ---------------
// DB-less boot uses a placeholder connection string that is never connected to (no readiness probe).
builder.Services.AddSyncInfrastructure(hasDatabase
    ? connectionString!
    : "Host=localhost;Port=5432;Database=momentum;Username=momentum;Password=not_configured");
// GOREV slice-3c D0 (IS-EMRI-o83 s2.1/5/7 ile GENISLETILDI): ICurrentUser artik ONCELIKLE JWT'den
// okur (her ortamda) -- eski deny-by-default NullCurrentUser'in yerini JwtCurrentUser alir (bir
// gecerli JWT yoksa AYNI sekilde null doner, deny-by-default korunur). Development'ta EK OLARAK
// X-Momentum-Dev-User basligi IKINCI/yedek yol olarak kalir (paket.yml AYAK 3 sozlesmesi -- s2.1/7,
// DOKUNMA listesi s5). AddHttpContextAccessor JwtCurrentUser/DevCurrentUser'in ikisinin de ortak
// gereksinimi.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<JwtCurrentUser>();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<DevCurrentUser>();
    builder.Services.AddScoped<ICurrentUser, CompositeCurrentUser>();
}
else
{
    builder.Services.AddScoped<ICurrentUser, JwtCurrentUser>();       // deny-by-default when unauthenticated
}
builder.Services.AddScoped<IValidator<SyncRequest>, SyncRequestValidator>();
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>)); // K-G1

// --- IS-EMRI-o83 D1: kimlik dilimi (users/refresh_tokens + JWT bearer) -------------------------
// Jwt:Secret HER ortamda zorunludur (appsettings.Development.json belgeli bir demo degeri tasir,
// docker-compose.yml'deki POSTGRES_PASSWORD varsayilani ile AYNI gerekce) -- eksikse sessizce
// herkesi 401'e dusurmek yerine ACIKCA acilista PATLAR (K-D5 deny-by-default ruhu, ama ayni zamanda
// "yanlislikla imzasiz calisiyor" durumunu da engeller).
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.Secret))
{
    throw new InvalidOperationException("Jwt:Secret yapilandirilmamis (appsettings.{Environment}.json ya da Jwt__Secret ortam degiskeni).");
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtOptions.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtOptions.Secret)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
});

builder.Services.AddAuthInfrastructure(); // IUserStore/IRefreshTokenStore (Infrastructure/Auth)
builder.Services.AddScoped<Momentum.Application.Abstractions.Auth.IPasswordHasher, AspNetPasswordHasher>();
builder.Services.AddScoped<IAccessTokenIssuer, JwtAccessTokenIssuer>();
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<RefreshRequest>, RefreshRequestValidator>();
builder.Services.AddScoped<IValidator<LogoutRequest>, LogoutRequestValidator>();

// --- slice-2b2: realtime signal (K2-F2/G1/G3). ISignalPublisher binds to SignalR only HERE -- the
//     dispatcher (Infrastructure) and the Domain/Application layers never see a SignalR type (D9-b). ---
builder.Services.AddSignalR();
builder.Services.AddScoped<ISignalPublisher, SignalRSignalPublisher>();
builder.Services.AddSingleton<OutboxDispatcherOptions>(); // production default: BatchSize=100, Lease=30s
if (hasDatabase)
{
    // D2-e: registered ONLY when a real connection string exists, so the DB-less slice-1 host (used by
    // Api.Tests' 9 tests) never starts a dispatcher that would immediately fault its DB calls.
    builder.Services.AddHostedService<OutboxDispatcher>();
}

// --- ADR 0001 K-D3: uniform error model (ProblemDetails / RFC 9457) in ALL environments -------
builder.Services.AddProblemDetails();

// --- ADR 0001 K-D1: OpenAPI document (built-in .NET 9 JSON generator) -> /openapi/v1.json ------
builder.Services.AddOpenApi();

// --- ADR 0001 K-D4: URL-segment API versioning (/v1) -----------------------------------------
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// --- ADR 0001 K-D2 + slice-2b1 D8: health checks. "self" is live+ready; the Npgsql probe is added to
//     "ready" ONLY when a DB is configured (so the DB-less slice-1 host stays ready). ----------------
var healthChecks = builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Process is up."), tags: ["live", "ready"]);
if (hasDatabase)
{
    healthChecks.AddCheck<NpgsqlReadyHealthCheck>("postgres", tags: ["ready"]);
}

// GOREV-W1 D-W1-1: allow-list is read from configuration, never a bare literal in code; an empty
// list means the CORS policy plain does not exist (no AllowAnyOrigin/SetIsOriginAllowed fallback).
var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

// W1/D-W1-2: CORS registration is Development-only (K-D5-style deny-by-default posture) -- this is
// a SEPARATE IsDevelopment() block from the one at :49 (K61 dev-identity shield); the marker comment
// exists because a dosya-geneli arama cannot tell the two blocks apart (denetim BLOKER-3).
if (builder.Environment.IsDevelopment() && corsAllowedOrigins.Length > 0) // W1/D-W1-2
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(corsAllowedOrigins)
        .WithHeaders("Content-Type", "X-Momentum-Dev-User")
        .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")));
}

var app = builder.Build();

// --- W3 yuruyen iskelet: capraz-koken izolasyon basliklari (COOP/COEP) --------------------
// EN USTTE durur: UseCors, UseExceptionHandler ve tum uc noktalar dahil HER yanit izole edilir.
// Kill switch "Izolasyon:Etkin" (varsayilan true); kapaliyken ara katman HIC kurulmaz.
var izolasyonEtkin = builder.Configuration.GetValue(IzolasyonBasliklari.EtkinAnahtari, true);
app.UseIzolasyonBasliklari(izolasyonEtkin);

// --- W3: istemci AYNI KOKENDEN sunulur (izolasyon basliklarindan HEMEN SONRA) ------------
// Sira zorunludur: COOP/COEP belgeyi ancak o belgenin KENDI yaniti tasirsa izole eder; statik
// dosya ara katmani izolasyon ara katmaninin ARDINDAN gelmezse istemci belgesi basliksiz iner.
// Kok dizin yapilandirmadan okunur (W1/D-W1-1 deseni); bos ya da yoksa ara katman HIC kurulmaz.
var istemciKokDizini = builder.Configuration[IstemciServisi.KokDizinAnahtari];
app.UseIstemciServisi(istemciKokDizini);

// W3/D-W3-2: UseRouting ACIKCA burada cagrilir -- WebApplication onu normalde ARDISIK HATTIN EN
// BASINA kendisi ekler ve o zaman statik dosya ara katmani yonlendirmeden SONRA kosar.
// OLCULMUS SONUC (oturum 63): StaticFileMiddleware, ESLESMIS bir uc nokta varsa dosyayi SUNMAZ
// (ValidateNoEndpoint); SPA geri-dususu `/{*path:nonfile}` UZANTISIZ her yolu esledigi icin
// `assets/NOTICES` diskte dururken 200 + index.html donuyordu. Yonlendirmeyi statikten SONRAYA
// almak bunu kapatir. UseCors HALA UseRouting ile uc nokta yurutmesi ARASINDADIR (D-W1-3 korunur).
app.UseRouting();

// W1/D-W1-2: mirrors the registration guard above (same corsAllowedOrigins captured by closure) --
// D-W1-3: UseCors must run before endpoint execution; there is no UseRouting call in this file, so
// middleware order is invisible at compile time and a wrong position would only fail at request time.
if (builder.Environment.IsDevelopment() && corsAllowedOrigins.Length > 0) // W1/D-W1-2
{
    app.UseCors();
}

// IS-EMRI-o83 D1: populates HttpContext.User from a valid bearer JWT -- must run before anything that
// reads ICurrentUser (JwtCurrentUser/CompositeCurrentUser above, and the /hubs/sync guard below).
// No UseAuthorization(): nothing here uses [Authorize]/RequireAuthorization, every endpoint still does
// its own explicit `currentUser.UserId is null -> 401` check (unchanged deny-by-default pattern).
app.UseAuthentication();

// Exception -> ProblemDetails, active in every environment (no Developer Exception Page leak).
app.UseExceptionHandler();

// Correlation-id (K-G3): honor an incoming X-Correlation-Id or mint one; echo it on the
// response and push it onto the Serilog log scope for every downstream log line.
app.Use(async (context, next) =>
{
    const string headerName = "X-Correlation-Id";
    var correlationId = context.Request.Headers.TryGetValue(headerName, out var incoming)
        && !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : Guid.NewGuid().ToString();

    context.Response.Headers[headerName] = correlationId;
    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next(context);
    }
});

app.UseSerilogRequestLogging();

// ADR 0001 K-D5: deny-by-default fallback policy -> auth slice. There is no authentication
// scheme yet, so enforcing a fallback authorization policy here would break the host; health
// and OpenAPI/Scalar stay anonymous until the identity slice wires auth in.

// slice-2b2 D4 (Onur kilidi #2): /hubs/sync deny-by-default. [Authorize] cannot be used (no auth scheme
// is registered -> it would 500, not 401). This is a plain path-based middleware, NOT AddEndpointFilter
// (SignalR's endpoint is not a route-handler/controller builder, so endpoint filters do not apply to
// it). Rejecting HERE -- before negotiate -- is what makes HubConnection.StartAsync actually throw
// (Hub.OnConnectedAsync's Context.Abort() runs AFTER the handshake already succeeded, D4 gerekce).
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/hubs/sync"))
    {
        var currentUser = context.RequestServices.GetRequiredService<ICurrentUser>();
        if (currentUser.UserId is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }

    await next(context);
});

// K-D1: OpenAPI JSON + Scalar UI, mapped in every environment (no auth in this slice -> no leak).
app.MapOpenApi();            // -> /openapi/v1.json
app.MapScalarApiReference(); // -> /scalar/v1

// K-D4: single version set for the business endpoints (/v1).
ApiVersionSet versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .Build();

// Endpoints live in named static classes (arch rule 2; GOREV slice-1 D3).
HealthEndpoints.Map(app);
DiagnosticsEndpoints.Map(app, versionSet);
AuthEndpoints.Map(app, versionSet); // IS-EMRI-o83 D1: POST /v1/auth/{register,login,refresh,logout}
UserLookupEndpoints.Map(app, versionSet); // IS-EMRI-o86-A §F: POST /v1/users/lookup
SyncEndpoints.Map(app, versionSet); // slice-2b1: POST /v1/sync
TaskEndpoints.Map(app, versionSet); // slice-3a D4: GET /v1/tasks(/{id})
TaskListEndpoints.Map(app, versionSet); // slice-3a D4: GET /v1/task-lists
ProjectEndpoints.Map(app, versionSet); // IS-EMRI-o85-B: GET /v1/projects (vitrin/API paritesi, istemci baglanmaz)

app.MapHub<SyncHub>("/hubs/sync"); // slice-2b2 D4: payload-less realtime signal

app.Run();

/// <summary>Exposed as partial so <c>WebApplicationFactory&lt;Program&gt;</c> can host it in tests.</summary>
public partial class Program;
