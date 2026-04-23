using BocconiLMS.Data;
using BocconiLMS.Services;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("MySQL")
    ?? "Server=localhost;Port=3306;Database=bocconi_lms;User=root;Password=;";

builder.Services.AddSingleton<DbHelper>(_ => new DbHelper(connectionString));
builder.Services.AddScoped<CourseRepository>();
builder.Services.AddScoped<LessonRepository>();
builder.Services.AddScoped<DocumentRepository>();
builder.Services.AddScoped<QuizRepository>();
builder.Services.AddScoped<EnrollmentRepository>();
builder.Services.AddScoped<ProgressRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<SettingsRepository>();
builder.Services.AddScoped<TranslationRepository>();

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

builder.Services.AddControllersWithViews();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Ensure password_reset_tokens table exists (applied automatically alongside schema.sql)
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var cmd = new MySqlConnector.MySqlCommand(@"
        CREATE TABLE IF NOT EXISTS password_reset_tokens (
            id          INT AUTO_INCREMENT PRIMARY KEY,
            user_id     INT NOT NULL,
            token       VARCHAR(64) NOT NULL,
            expires_at  DATETIME NOT NULL,
            used        TINYINT(1) NOT NULL DEFAULT 0,
            created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY uk_token (token),
            FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
            INDEX idx_token (token),
            INDEX idx_user  (user_id)
        ) ENGINE=InnoDB;", conn);
    await cmd.ExecuteNonQueryAsync();
}
catch
{
    // Database may not be configured yet; skip silently
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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");

public partial class Program { }
