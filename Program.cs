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
builder.Services.AddScoped<CourseRepository>();
builder.Services.AddScoped<LessonRepository>();
builder.Services.AddScoped<QuizRepository>();
builder.Services.AddScoped<EnrollmentRepository>();
builder.Services.AddScoped<ProgressRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<SettingsRepository>();
builder.Services.AddScoped<TranslationRepository>();
builder.Services.AddScoped<MaterialRepository>();
builder.Services.AddScoped<DocumentTypeRepository>();
builder.Services.AddScoped<AreaRepository>();
builder.Services.AddScoped<PlatformRepository>();
builder.Services.AddScoped<RolePermissionRepository>();
builder.Services.AddScoped<FeatureFlagService>();
builder.Services.AddScoped<MigrationRunner>();
builder.Services.AddScoped<ProductionScriptGenerator>();

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

builder.Services.AddControllersWithViews();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ── Apply all pending database migrations ─────────────────────────────────────
// Fail-fast: any migration error stops the application from starting.
{
    using var scope = app.Services.CreateScope();
    var runner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Running database migrations...");
    await runner.RunAsync();   // throws MigrationException on failure → app stops
    logger.LogInformation("Database migrations complete.");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseMiddleware<HttpAccessLogMiddleware>();
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");

public partial class Program { }
