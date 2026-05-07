using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using BocconiLMS.Data;
using BocconiLMS.Middleware;
using BocconiLMS.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Allow up to 500 MB uploads (for video files)
builder.WebHost.ConfigureKestrel(k =>
    k.Limits.MaxRequestBodySize = 500 * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 500 * 1024 * 1024;
    o.ValueLengthLimit = int.MaxValue;
});

var connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("MySQL")
    ?? "Server=localhost;Port=3306;Database=bocconi_lms;User=root;Password=;";

builder.Services.AddHttpClient();
builder.Services.AddSingleton<DbHelper>(_ => new DbHelper(connectionString));
builder.Services.AddSingleton<SystemLogRepository>();
builder.Services.AddScoped<ApiKeyRepository>();
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddScoped<CourseRepository>();
builder.Services.AddScoped<LessonRepository>();
builder.Services.AddScoped<QuizRepository>();
builder.Services.AddScoped<EnrollmentRepository>();
builder.Services.AddScoped<ProgressRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<SettingsRepository>();
builder.Services.AddScoped<TranslationRepository>();
builder.Services.AddScoped<MaterialRepository>();
builder.Services.AddScoped<LessonGroupRepository>();
builder.Services.AddScoped<DocumentTypeRepository>();
builder.Services.AddScoped<AreaRepository>();
builder.Services.AddScoped<PlatformRepository>();
builder.Services.AddScoped<RolePermissionRepository>();
builder.Services.AddScoped<FeatureFlagService>();
builder.Services.AddScoped<ProductionScriptGenerator>();
builder.Services.AddScoped<DataImportService>();

builder.Services.AddScoped<IUserStore<ApplicationUser>, CustomUserStore>();
builder.Services.AddScoped<IRoleStore<ApplicationRole>, CustomRoleStore>();

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddDefaultTokenProviders();

builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, BcryptPasswordHasher>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<EmailService>();
builder.Services.AddHostedService<LessonReminderHostedService>();

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TranslationService>();
builder.Services.AddSingleton<AppVersionService>();
builder.Services.AddSingleton<IAuditLogger, AuditLogger>();

builder.Services.AddHealthChecks()
    .AddCheck<MySqlHealthCheck>("database", tags: ["db"]);

builder.Services.AddSingleton<Microsoft.Extensions.Localization.IStringLocalizerFactory,
    BocconiLMS.Services.DbStringLocalizerFactory>();
builder.Services.AddControllersWithViews()
    .AddDataAnnotationsLocalization();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Il cookie di stato Sustainsys deve sopravvivere al redirect cross-site
// dall'IdP (POST binding). SameSite=Lax (default .NET) viene scartato dal
// browser su POST cross-origin → l'ACS non trova lo stato → UnexpectedInResponseToException.
// La cookie policy intercetta l'Append e forza SameSite=None sui cookie Sustainsys.
builder.Services.Configure<CookiePolicyOptions>(cookiePolicy =>
{
    cookiePolicy.MinimumSameSitePolicy = SameSiteMode.Unspecified;
    cookiePolicy.OnAppendCookie = ctx =>
    {
        if (ctx.CookieName.StartsWith("Sustainsys", StringComparison.OrdinalIgnoreCase))
        {
            ctx.CookieOptions.SameSite = SameSiteMode.None;
            // Secure=true solo se la connessione è già HTTPS (in dev HTTP funziona comunque)
            ctx.CookieOptions.Secure = ctx.Context.Request.IsHttps;
        }
    };
});

// ── Shibboleth / SAML 2.0 SSO ──────────────────────────────────────────────
// Development fallback: Sustainsys StubIdP — progettato per testare questa libreria,
// non richiede registrazione SP e funziona out-of-the-box.
// samltest.id è stato abbandonato: restituisce 400 Bad Request sul metadata endpoint.
// Lo StubIdP usa lo stesso valore per entityID e metadata URL
const string StubIdpEntityId  = "https://stubidp.sustainsys.com/Metadata";
const string StubIdpMetaUrl   = "https://stubidp.sustainsys.com/Metadata";

// SAML_IDP_METADATA_URL  → URL da cui scaricare il metadata XML dell'IdP
//   dev default : https://stubidp.sustainsys.com/Metadata
//   Bocconi prod: https://idp.unibocconi.it/metadata/get-config.php?what=UNIBOCCONI-ADFS
var samlIdpMetadataUrl = Environment.GetEnvironmentVariable("SAML_IDP_METADATA_URL")
                      ?? StubIdpMetaUrl;

// SAML_IDP_ENTITY_ID → entityID nelle asserzioni SAML (può differire dalla metadata URL)
//   dev default : https://stubidp.sustainsys.com/
//   Bocconi prod: https://idp.unibocconi-prod.it/idp/shibboleth
var samlIdpEntityId = Environment.GetEnvironmentVariable("SAML_IDP_ENTITY_ID")
                   ?? (string.Equals(samlIdpMetadataUrl, StubIdpMetaUrl,
                          StringComparison.OrdinalIgnoreCase)
                       ? StubIdpEntityId
                       : samlIdpMetadataUrl);

// SP identity & public origin
var samlSpEntityId = Environment.GetEnvironmentVariable("SAML_SP_ENTITY_ID")
                  ?? "https://didasco.unibocconi.it";
var samlBaseUrl    = Environment.GetEnvironmentVariable("SAML_SP_BASE_URL");

// Fail-fast guard: in Production DEVE essere impostato l'IdP Bocconi reale.
// In Development e Staging (es. IIS locale) si permette l'uso di IdP di test
// ma viene loggato un warning visibile nella console IIS/Event Viewer.
static bool IsTestIdp(string url) =>
    string.Equals(url, "https://stubidp.sustainsys.com/Metadata", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(url, "https://samltest.id/saml/idp",            StringComparison.OrdinalIgnoreCase);

if (IsTestIdp(samlIdpMetadataUrl))
{
    if (builder.Environment.IsProduction())
        throw new InvalidOperationException(
            "SAML_IDP_METADATA_URL deve puntare all'IdP Bocconi reale in ambiente Production. " +
            "Impostare la variabile d'ambiente SAML_IDP_METADATA_URL.");

    // Development / Staging / Testing / IIS locale: avvisa ma non blocca
    Console.WriteLine(
        "[SAML WARNING] Ambiente non-Production: si usa un IdP di test. " +
        "Non adatto per dati reali Bocconi.");
}

builder.Services.AddAuthentication()
    .AddSaml2(options =>
    {
        options.SignInScheme = Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme;
        options.SPOptions.EntityId = new Sustainsys.Saml2.Metadata.EntityId(samlSpEntityId);
        // PublicOrigin determina l'ACS URL inviato all'IdP.
        // Se SAML_SP_BASE_URL è esplicito → usarlo sempre.
        // In Development senza variabile → NON impostarlo: Sustainsys lo ricava
        //   dall'URL reale della richiesta HTTP (funziona sia con localhost:5000
        //   che con https://didasco.local senza nessuna config aggiuntiva).
        // In produzione senza variabile → usare l'entity ID come fallback.
        if (!string.IsNullOrEmpty(samlBaseUrl))
            options.SPOptions.PublicOrigin = new Uri(samlBaseUrl);
        else if (!builder.Environment.IsDevelopment())
            options.SPOptions.PublicOrigin = new Uri(samlSpEntityId);

        // ── SP signing certificate ──────────────────────────────────────────
        // Strategy (in order of priority):
        //   1. SAML_SP_CERT_PFX secret  → base64-encoded PKCS#12 bundle (cert + key)
        //   2. No secret                → generate a fresh RSA-2048 self-signed cert
        //      Works perfectly in dev/Replit; in prod use SAML_SP_CERT_PFX.
        //
        // NOTE: raw PEM-from-base64 was abandoned because .NET's CreateFromPem is
        //       strict about BOM/invisible chars that may be introduced by secret UIs.
        X509Certificate2 spCert;
        var spCertPfxB64 = Environment.GetEnvironmentVariable("SAML_SP_CERT_PFX");
        if (!string.IsNullOrEmpty(spCertPfxB64))
        {
            var pfxBytes = Convert.FromBase64String(
                spCertPfxB64.Replace("\r","").Replace("\n","").Replace(" ","").Trim());
            spCert = X509CertificateLoader.LoadPkcs12(pfxBytes, password: null,
                X509KeyStorageFlags.EphemeralKeySet);
        }
        else
        {
            // Generate a fresh self-signed cert at startup (dev / Replit default)
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest(
                "CN=didasco.unibocconi.it, O=Universita Bocconi, C=IT",
                rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            spCert = req.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddYears(10));
        }
        options.SPOptions.ServiceCertificates.Add(
            new Sustainsys.Saml2.ServiceCertificate
            {
                Certificate = spCert,
                Use         = Sustainsys.Saml2.CertificateUse.Signing
            });

        // ── IdP registration ────────────────────────────────────────────────
        // Entity ID and metadata fetch URL are deliberately kept separate:
        // for Bocconi they point to different hostnames.
        var idp = new Sustainsys.Saml2.IdentityProvider(
            new Sustainsys.Saml2.Metadata.EntityId(samlIdpEntityId),
            options.SPOptions)
        {
            MetadataLocation              = samlIdpMetadataUrl,
            LoadMetadata                  = true,
            AllowUnsolicitedAuthnResponse = false
        };
        options.IdentityProviders.Add(idp);
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseCookiePolicy();   // deve precedere UseAuthentication per intercettare i cookie Sustainsys
app.UseSession();
app.UseAuthentication();
app.UseMiddleware<HttpAccessLogMiddleware>();
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
        var status = report.Status == HealthStatus.Healthy ? "healthy"
                   : report.Status == HealthStatus.Degraded ? "degraded"
                   : "unhealthy";

        var result = new
        {
            status,
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
            duration_ms = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString().ToLower(),
                description = e.Value.Description,
                duration_ms = (int)e.Value.Duration.TotalMilliseconds,
                error = e.Value.Exception?.Message
            })
        };

        logger.LogInformation("[$HEALTH-CHECK] status={Status} duration_ms={Duration}",
            status, (int)report.TotalDuration.TotalMilliseconds);

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(result,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}).AllowAnonymous();

{
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    startupLogger.LogInformation("[$HEALTH-CHECK] registered path=/health");
}

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Alias per SP metadata richiesto dalle specifiche Bocconi IT
// (/Saml2/metadata è l'endpoint nativo di Sustainsys)
app.MapGet("/auth/saml-metadata", (HttpContext ctx) =>
{
    ctx.Response.Redirect("/Saml2/metadata", permanent: false);
    return Task.CompletedTask;
}).AllowAnonymous();

// IIS detection: AspNetCoreModuleV2 sets these env vars depending on hosting mode.
//   - inprocess:    ASPNETCORE_IIS_HTTPAUTH, IIS_USER_TOKEN
//   - outofprocess: ASPNETCORE_PORT, ASPNETCORE_TOKEN, ANCM_HTTP_PORT
// In either case the IIS module owns the binding; the app must NOT call Run(url).
var isIIS = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_IIS_HTTPAUTH"))
         || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IIS_USER_TOKEN"))
         || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_TOKEN"))
         || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANCM_HTTP_PORT"));

if (isIIS)
{
    app.Run();
}
else
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
    app.Run($"http://0.0.0.0:{port}");
}

public partial class Program { }
