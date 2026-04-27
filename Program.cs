using BocconiLMS.Data;
using BocconiLMS.Services;
using Microsoft.AspNetCore.Identity;

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
builder.Services.AddScoped<MaterialRepository>();
builder.Services.AddScoped<DocumentTypeRepository>();
builder.Services.AddScoped<FeatureFlagService>();

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

// Apply incremental schema migrations
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();

    // Add created_at to translations if missing (compatible with MySQL 5.7+)
    using var colCheck = new MySqlConnector.MySqlCommand(@"
        SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME   = 'translations'
          AND COLUMN_NAME  = 'created_at';", conn);
    var colExists = Convert.ToInt32(await colCheck.ExecuteScalarAsync()) > 0;
    if (!colExists)
    {
        using var addCol = new MySqlConnector.MySqlCommand(
            "ALTER TABLE translations ADD COLUMN created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP;", conn);
        await addCol.ExecuteNonQueryAsync();

        // Back-fill to today for all existing rows
        using var backfill = new MySqlConnector.MySqlCommand(
            "UPDATE translations SET created_at = NOW() WHERE created_at < '2020-01-01';", conn);
        await backfill.ExecuteNonQueryAsync();
    }
}
catch { }

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

// ── Materials library: create tables + seed document_types ──────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();

    // document_types
    using var c1 = new MySqlConnector.MySqlCommand(@"
        CREATE TABLE IF NOT EXISTS document_types (
            id         INT AUTO_INCREMENT PRIMARY KEY,
            name       VARCHAR(255) NOT NULL UNIQUE,
            sort_order INT NOT NULL DEFAULT 0
        ) ENGINE=InnoDB;", conn);
    await c1.ExecuteNonQueryAsync();

    // seed predefined types
    using var c2 = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO document_types (name, sort_order) VALUES
        ('Allegati',1),('Articolo non pubblicato',2),('Atti del Convegno',3),
        ('Caso',4),('Esercitazione',5),('Incident',6),('Manuale',7),
        ('Materiale audiovisivo',8),('Norme e Leggi',9),('Nota',10),
        ('Paper',11),('Questionario',12),('Report di Ricerca',13),
        ('Role Playing - Simulazione',14),('Scheda - Griglia',15),
        ('SDA Case Collection / ECCH',16),
        ('SDA Case Collection Background Note / ECCH',17),
        ('SDA Case Collection Instructor Spreadsheet / ECCH',18),
        ('SDA Case Collection Role Playing / ECCH',19),
        ('SDA Case Collection Slide / ECCH',20),
        ('SDA Case Collection Supplementary software / ECCH',21),
        ('SDA Case Collection Teaching Notes / ECCH',22),
        ('SDA Case Collection Teaching Notes Supplement software / ECCH',23),
        ('SDA Case Collection Instructor presentation material / ECCH',24),
        ('Slides',25),('Soluzione caso',26),('Teaching Notes',27),
        ('Traduzione autorizzata articoli e capitoli',28),
        ('Traduzione autorizzata caso',29);", conn);
    await c2.ExecuteNonQueryAsync();

    // materials
    using var c3 = new MySqlConnector.MySqlCommand(@"
        CREATE TABLE IF NOT EXISTS materials (
            id               INT AUTO_INCREMENT PRIMARY KEY,
            title            VARCHAR(255) NOT NULL,
            owner_id         INT NULL,
            language         VARCHAR(50) NOT NULL DEFAULT 'Italiano',
            document_type_id INT NULL,
            created_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY uk_title (title),
            FOREIGN KEY (owner_id)         REFERENCES users(id)          ON DELETE SET NULL,
            FOREIGN KEY (document_type_id) REFERENCES document_types(id) ON DELETE SET NULL,
            INDEX idx_owner (owner_id),
            INDEX idx_type  (document_type_id)
        ) ENGINE=InnoDB;", conn);
    await c3.ExecuteNonQueryAsync();

    // material_versions
    using var c4 = new MySqlConnector.MySqlCommand(@"
        CREATE TABLE IF NOT EXISTS material_versions (
            id              INT AUTO_INCREMENT PRIMARY KEY,
            material_id     INT NOT NULL,
            version_number  INT NOT NULL,
            file_name       VARCHAR(255) NOT NULL,
            file_path       VARCHAR(500) NOT NULL,
            file_type       VARCHAR(20)  NOT NULL,
            file_size_bytes BIGINT NOT NULL DEFAULT 0,
            uploaded_by     INT NOT NULL,
            notes           TEXT,
            is_active       TINYINT(1) NOT NULL DEFAULT 1,
            uploaded_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (material_id) REFERENCES materials(id) ON DELETE CASCADE,
            FOREIGN KEY (uploaded_by) REFERENCES users(id),
            UNIQUE KEY uniq_ver (material_id, version_number),
            INDEX idx_material (material_id),
            INDEX idx_active   (material_id, is_active)
        ) ENGINE=InnoDB;", conn);
    await c4.ExecuteNonQueryAsync();

    // lesson_materials
    using var c5 = new MySqlConnector.MySqlCommand(@"
        CREATE TABLE IF NOT EXISTS lesson_materials (
            lesson_id   INT NOT NULL,
            material_id INT NOT NULL,
            added_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            added_by    INT NULL,
            PRIMARY KEY (lesson_id, material_id),
            FOREIGN KEY (lesson_id)   REFERENCES lessons(id)   ON DELETE CASCADE,
            FOREIGN KEY (material_id) REFERENCES materials(id) ON DELETE CASCADE,
            FOREIGN KEY (added_by)    REFERENCES users(id)     ON DELETE SET NULL
        ) ENGINE=InnoDB;", conn);
    await c5.ExecuteNonQueryAsync();
}
catch { }

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
