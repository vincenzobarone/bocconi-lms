using BocconiLMS.Data;
using BocconiLMS.Services;
using Microsoft.AspNetCore.Identity;
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
builder.Services.AddScoped<RolePermissionRepository>();
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

// ── Seed Admin platform-settings translation keys ───────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        ('en','admin.platform_settings','Platform Settings'),
        ('en','admin.platform_settings_desc','Platform configuration settings'),
        ('en','admin.configure','Configure'),
        ('en','admin.doc_types','Document Types'),
        ('en','admin.doc_types_desc','Manage document types available in the Materials library.'),
        ('en','admin.manage_types','Manage types'),
        ('en','admin.users_roles','Users & Roles'),
        ('en','admin.users_roles_desc','Manage user accounts, permissions and roles.'),
        ('en','admin.manage_users','Manage users'),
        ('en','admin.manage_roles','Manage roles'),
        ('it','admin.platform_settings','Impostazioni Piattaforma'),
        ('it','admin.platform_settings_desc','Impostazioni di configurazione della piattaforma'),
        ('it','admin.configure','Configura'),
        ('it','admin.doc_types','Tipi Documento'),
        ('it','admin.doc_types_desc','Gestisci l''elenco dei tipi di documento disponibili nella libreria Materiali.'),
        ('it','admin.manage_types','Gestisci tipi'),
        ('it','admin.users_roles','Utenti e Ruoli'),
        ('it','admin.users_roles_desc','Gestisci account, permessi e ruoli degli utenti della piattaforma.'),
        ('it','admin.manage_users','Gestisci utenti'),
        ('it','admin.manage_roles','Gestisci ruoli');", conn);
    await ins.ExecuteNonQueryAsync();

    foreach (var lang in new[] { "es", "de" })
    {
        using var copy = new MySqlConnector.MySqlCommand(@"
            INSERT IGNORE INTO translations (language_code, label_key, label_value)
            SELECT @lang, label_key, label_value FROM translations
            WHERE language_code = 'en'
              AND label_key IN ('admin.platform_settings','admin.platform_settings_desc',
                                'admin.configure','admin.doc_types','admin.doc_types_desc',
                                'admin.manage_types','admin.users_roles','admin.users_roles_desc',
                                'admin.manage_users','admin.manage_roles');", conn);
        copy.Parameters.AddWithValue("@lang", lang);
        await copy.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Seed Materials translation keys (EN + IT + copy to ES/DE) ───────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();

    // Only seed if mat.page_title doesn't exist yet
    using var check = new MySqlConnector.MySqlCommand(
        "SELECT COUNT(*) FROM translations WHERE language_code='en' AND label_key='mat.page_title';", conn);
    var exists = Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;

    if (!exists)
    {
        using var ins = new MySqlConnector.MySqlCommand(@"
            INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
            ('en','mat.nav','Materials'),
            ('en','mat.page_title','Materials Library'),
            ('en','mat.new_btn','New material'),
            ('en','mat.back','Back to Materials'),
            ('en','mat.filter_title','Title'),
            ('en','mat.filter_lang','Language'),
            ('en','mat.filter_type','Document type'),
            ('en','mat.search_placeholder','Search by title…'),
            ('en','mat.all_langs','— All —'),
            ('en','mat.all_types','— All —'),
            ('en','mat.col_type','Type'),
            ('en','mat.col_author','Author'),
            ('en','mat.col_version','Ver.'),
            ('en','mat.col_lang','Language'),
            ('en','mat.col_created','Created'),
            ('en','mat.details_btn','Details'),
            ('en','mat.download_btn','Download active version'),
            ('en','mat.no_results','No materials found.'),
            ('en','mat.create_first','Create the first material'),
            ('en','mat.no_results_student','No materials available at the moment.'),
            ('en','mat.student_readonly','You can browse and download materials. For changes or uploads, contact your teacher.'),
            ('en','mat.versions','Versions'),
            ('en','mat.no_files','No file uploaded yet.'),
            ('en','mat.version_active','Active'),
            ('en','mat.restore_btn','Restore'),
            ('en','mat.restore_confirm','Restore version'),
            ('en','mat.upload_version','Upload new version'),
            ('en','mat.notes','Notes'),
            ('en','mat.notes_placeholder','What changed?'),
            ('en','mat.upload_btn','Upload'),
            ('en','mat.info_panel','Information'),
            ('en','mat.create_title','New material'),
            ('en','mat.edit_title','Edit material'),
            ('en','mat.edit_breadcrumb','Edit'),
            ('en','mat.label_title','Title'),
            ('en','mat.title_placeholder','Enter a unique title…'),
            ('en','mat.label_doctype','Document type'),
            ('en','mat.select_type','— Select a type —'),
            ('en','mat.label_language','Language'),
            ('en','mat.label_owner','Author / Owner'),
            ('en','mat.no_owner','— None —'),
            ('en','mat.owner_hint','Who is responsible for the material.'),
            ('en','mat.choose_file','Choose file'),
            ('en','mat.no_file_chosen','No file chosen'),
            ('en','mat.file_optional','File (optional — can be uploaded later)'),
            ('en','mat.label_file','File'),
            ('en','mat.label_notes','Version notes'),
            ('en','mat.notes_file_placeholder','File description (optional)'),
            ('en','mat.cancel','Cancel'),
            ('en','mat.create_btn','Create material'),
            ('en','mat.upload_new_section','Upload new version file (optional)'),
            ('en','mat.active_version_label','Active version:'),
            ('en','mat.new_file','New file'),
            ('en','mat.file_hint','Leave empty to not update the file.'),
            ('en','mat.new_version_notes','New version notes'),
            ('en','mat.version_notes_placeholder','What changes in this version?'),
            ('en','mat.save_btn','Save changes'),
            ('it','mat.nav','Materiali'),
            ('it','mat.page_title','Libreria Materiali'),
            ('it','mat.new_btn','Nuovo materiale'),
            ('it','mat.back','Torna ai Materiali'),
            ('it','mat.filter_title','Titolo'),
            ('it','mat.filter_lang','Lingua'),
            ('it','mat.filter_type','Tipo documento'),
            ('it','mat.search_placeholder','Cerca per titolo…'),
            ('it','mat.all_langs','— Tutte —'),
            ('it','mat.all_types','— Tutti —'),
            ('it','mat.col_type','Tipo'),
            ('it','mat.col_author','Autore'),
            ('it','mat.col_version','Ver.'),
            ('it','mat.col_lang','Lingua'),
            ('it','mat.col_created','Creato'),
            ('it','mat.details_btn','Dettagli'),
            ('it','mat.download_btn','Scarica versione attiva'),
            ('it','mat.no_results','Nessun materiale trovato.'),
            ('it','mat.create_first','Crea il primo materiale'),
            ('it','mat.no_results_student','Nessun materiale disponibile al momento.'),
            ('it','mat.student_readonly','Puoi sfogliare e scaricare i materiali. Per modifiche o upload contatta il tuo docente.'),
            ('it','mat.versions','Versioni'),
            ('it','mat.no_files','Nessun file caricato ancora.'),
            ('it','mat.version_active','Attiva'),
            ('it','mat.restore_btn','Ripristina'),
            ('it','mat.restore_confirm','Ripristinare la versione'),
            ('it','mat.upload_version','Carica nuova versione'),
            ('it','mat.notes','Note'),
            ('it','mat.notes_placeholder','Cosa cambia?'),
            ('it','mat.upload_btn','Carica'),
            ('it','mat.info_panel','Informazioni'),
            ('it','mat.create_title','Nuovo materiale'),
            ('it','mat.edit_title','Modifica materiale'),
            ('it','mat.edit_breadcrumb','Modifica'),
            ('it','mat.label_title','Titolo'),
            ('it','mat.title_placeholder','Inserisci un titolo univoco…'),
            ('it','mat.label_doctype','Tipo documento'),
            ('it','mat.select_type','— Seleziona un tipo —'),
            ('it','mat.label_language','Lingua'),
            ('it','mat.label_owner','Autore / Responsabile'),
            ('it','mat.no_owner','— Nessuno —'),
            ('it','mat.owner_hint','Chi è responsabile del materiale.'),
            ('it','mat.choose_file','Scegli file'),
            ('it','mat.no_file_chosen','Nessun file scelto'),
            ('it','mat.file_optional','File (opzionale — può essere caricato in seguito)'),
            ('it','mat.label_file','File'),
            ('it','mat.label_notes','Note versione'),
            ('it','mat.notes_file_placeholder','Descrizione del file (opzionale)'),
            ('it','mat.cancel','Annulla'),
            ('it','mat.create_btn','Crea materiale'),
            ('it','mat.upload_new_section','Carica nuova versione file (opzionale)'),
            ('it','mat.active_version_label','Versione attiva:'),
            ('it','mat.new_file','Nuovo file'),
            ('it','mat.file_hint','Lascia vuoto per non aggiornare il file.'),
            ('it','mat.new_version_notes','Note nuova versione'),
            ('it','mat.version_notes_placeholder','Cosa cambia in questa versione?'),
            ('it','mat.save_btn','Salva modifiche');", conn);
        await ins.ExecuteNonQueryAsync();

        // Copy EN → ES and DE for missing keys
        foreach (var lang in new[] { "es", "de" })
        {
            using var copy = new MySqlConnector.MySqlCommand(@"
                INSERT IGNORE INTO translations (language_code, label_key, label_value)
                SELECT @lang, label_key, label_value FROM translations
                WHERE language_code = 'en' AND label_key LIKE 'mat.%';", conn);
            copy.Parameters.AddWithValue("@lang", lang);
            await copy.ExecuteNonQueryAsync();
        }
    }
}
catch { }

// ── Migrate: create areas + user_areas tables, seed default areas ─────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();

    using var ddl = new MySqlConnector.MySqlCommand(@"
        CREATE TABLE IF NOT EXISTS areas (
            id         INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            name       VARCHAR(255) NOT NULL,
            sort_order INT NOT NULL DEFAULT 0
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

        CREATE TABLE IF NOT EXISTS user_areas (
            user_id INT NOT NULL,
            area_id INT NOT NULL,
            PRIMARY KEY (user_id, area_id),
            CONSTRAINT fk_ua_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
            CONSTRAINT fk_ua_area FOREIGN KEY (area_id) REFERENCES areas(id) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;", conn);
    await ddl.ExecuteNonQueryAsync();

    // Seed default areas (only if table is empty)
    using var countCmd = new MySqlConnector.MySqlCommand("SELECT COUNT(*) FROM areas", conn);
    var areaCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
    if (areaCount == 0)
    {
        var defaultAreas = new[]
        {
            "Leadership, Human Resources and Digital Technologies",
            "Strategy and Operations",
            "Finance",
            "Accounting",
            "Government, Health and not for profit",
            "Economics, Politics and Decision Sciences",
            "Law",
            "Marketing"
        };
        int sortOrder = 1;
        foreach (var areaName in defaultAreas)
        {
            using var ins = new MySqlConnector.MySqlCommand(
                "INSERT IGNORE INTO areas (name, sort_order) VALUES (@n, @s)", conn);
            ins.Parameters.AddWithValue("@n", areaName);
            ins.Parameters.AddWithValue("@s", sortOrder++);
            await ins.ExecuteNonQueryAsync();
        }
    }
}
catch { }

// ── Seed Areas translation keys ────────────────────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        ('en','admin.areas_tab','Areas'),
        ('en','admin.add_area','Add new area'),
        ('en','admin.area_name_placeholder','Area name…'),
        ('en','admin.create_area','Create area'),
        ('en','admin.no_areas','No areas defined yet.'),
        ('en','admin.delete_area','Delete area'),
        ('en','admin.delete_area_confirm','Delete area'),
        ('en','admin.edit_area','Edit area'),
        ('it','admin.areas_tab','Aree'),
        ('it','admin.add_area','Aggiungi nuova area'),
        ('it','admin.area_name_placeholder','Nome area…'),
        ('it','admin.create_area','Crea area'),
        ('it','admin.no_areas','Nessuna area definita.'),
        ('it','admin.delete_area','Elimina area'),
        ('it','admin.delete_area_confirm','Eliminare l\'area'),
        ('it','admin.edit_area','Modifica area');", conn);
    await ins.ExecuteNonQueryAsync();
    foreach (var lang in new[] { "es", "de" })
    {
        using var copy = new MySqlConnector.MySqlCommand(@"
            INSERT IGNORE INTO translations (language_code, label_key, label_value)
            SELECT @lang, label_key, label_value FROM translations
            WHERE language_code = 'en'
              AND label_key IN (
                'admin.areas_tab','admin.add_area','admin.area_name_placeholder',
                'admin.create_area','admin.no_areas','admin.delete_area','admin.delete_area_confirm',
                'admin.edit_area');", conn);
        copy.Parameters.AddWithValue("@lang", lang);
        await copy.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Migrate: convert users.role from ENUM to VARCHAR(50) ─────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    // Check current column type; only alter if it's still ENUM
    using var check = new MySqlConnector.MySqlCommand(@"
        SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'users'
          AND COLUMN_NAME = 'role'", conn);
    var dataType = (await check.ExecuteScalarAsync())?.ToString();
    if (dataType?.Equals("enum", StringComparison.OrdinalIgnoreCase) == true)
    {
        using var alter = new MySqlConnector.MySqlCommand(
            "ALTER TABLE users MODIFY COLUMN role VARCHAR(50) NOT NULL DEFAULT 'Student';", conn);
        await alter.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Migrate: create role_permissions table ────────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ddl = new MySqlConnector.MySqlCommand(@"
        CREATE TABLE IF NOT EXISTS role_permissions (
            role_id        INT NOT NULL,
            permission_key VARCHAR(50) NOT NULL,
            PRIMARY KEY (role_id, permission_key),
            CONSTRAINT fk_rp_role FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;", conn);
    await ddl.ExecuteNonQueryAsync();
}
catch { }

// ── Seed permission translation keys ──────────────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        ('en','perm.materials_create','Create Materials'),
        ('en','perm.materials_edit','Edit Materials'),
        ('en','perm.materials_approve','Approve Materials'),
        ('en','perm.courses_teach','Teach a course'),
        ('en','perm.courses_enroll','Participate in a course'),
        ('it','perm.materials_create','Crea Materiali'),
        ('it','perm.materials_edit','Modifica Materiali'),
        ('it','perm.materials_approve','Approva Materiali'),
        ('it','perm.courses_teach','Sostieni corso'),
        ('it','perm.courses_enroll','Partecipa al corso'),
        ('es','perm.materials_create','Crear Materiales'),
        ('es','perm.materials_edit','Editar Materiales'),
        ('es','perm.materials_approve','Aprobar Materiales'),
        ('es','perm.courses_teach','Impartir un curso'),
        ('es','perm.courses_enroll','Participar en un curso'),
        ('de','perm.materials_create','Materialien erstellen'),
        ('de','perm.materials_edit','Materialien bearbeiten'),
        ('de','perm.materials_approve','Materialien genehmigen'),
        ('de','perm.courses_teach','Kurs unterrichten'),
        ('de','perm.courses_enroll','An einem Kurs teilnehmen');", conn);
    await ins.ExecuteNonQueryAsync();
}
catch { }

// ── Migrate: add author_name column to materials ──────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var chk = new MySqlConnector.MySqlCommand(@"
        SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME   = 'materials'
          AND COLUMN_NAME  = 'author_name'", conn);
    var exists = Convert.ToInt32(await chk.ExecuteScalarAsync()) > 0;
    if (!exists)
    {
        using var ddl = new MySqlConnector.MySqlCommand(
            "ALTER TABLE materials ADD COLUMN author_name VARCHAR(255) NULL AFTER title;", conn);
        await ddl.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Migrate: add folder column to materials ───────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var chk = new MySqlConnector.MySqlCommand(@"
        SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME   = 'materials'
          AND COLUMN_NAME  = 'folder'", conn);
    var exists = Convert.ToInt32(await chk.ExecuteScalarAsync()) > 0;
    if (!exists)
    {
        using var ddl = new MySqlConnector.MySqlCommand(
            "ALTER TABLE materials ADD COLUMN folder VARCHAR(255) NULL;", conn);
        await ddl.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Migrate: create material_folders table ────────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ddl = new MySqlConnector.MySqlCommand(@"
        CREATE TABLE IF NOT EXISTS material_folders (
            id         INT AUTO_INCREMENT PRIMARY KEY,
            name       VARCHAR(255) NOT NULL,
            created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY uk_name (name)
        ) ENGINE=InnoDB;", conn);
    await ddl.ExecuteNonQueryAsync();
}
catch { }

// ── Migrate: add folder_id column to materials ────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var chk = new MySqlConnector.MySqlCommand(@"
        SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME   = 'materials'
          AND COLUMN_NAME  = 'folder_id'", conn);
    if (Convert.ToInt32(await chk.ExecuteScalarAsync()) == 0)
    {
        using var ddl = new MySqlConnector.MySqlCommand(@"
            ALTER TABLE materials
            ADD COLUMN folder_id INT NULL,
            ADD CONSTRAINT fk_material_folder
                FOREIGN KEY (folder_id) REFERENCES material_folders(id) ON DELETE SET NULL;", conn);
        await ddl.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Migrate: change protocol_number from VARCHAR to INT NULL ──────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var chk = new MySqlConnector.MySqlCommand(@"
        SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME   = 'materials'
          AND COLUMN_NAME  = 'protocol_number'", conn);
    var dtype = await chk.ExecuteScalarAsync() as string;
    if (dtype != null && dtype.ToLower() != "int")
    {
        // NULL out non-numeric values before type change
        using var clr = new MySqlConnector.MySqlCommand(
            "UPDATE materials SET protocol_number = NULL WHERE protocol_number REGEXP '[^0-9]' OR protocol_number = '';", conn);
        await clr.ExecuteNonQueryAsync();
        using var alter = new MySqlConnector.MySqlCommand(
            "ALTER TABLE materials MODIFY COLUMN protocol_number INT NULL;", conn);
        await alter.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Seed material author + convert_to_pdf translation keys ────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        ('en','mat.convert_to_pdf','Convert to PDF before saving'),
        ('it','mat.convert_to_pdf','Converti in PDF prima del salvataggio'),
        ('es','mat.convert_to_pdf','Convertir a PDF antes de guardar'),
        ('de','mat.convert_to_pdf','Vor dem Speichern in PDF konvertieren'),
        ('en','mat.file_section','File'),
        ('it','mat.file_section','File'),
        ('es','mat.file_section','Archivo'),
        ('de','mat.file_section','Datei'),
        ('en','mat.label_author','Author'),
        ('it','mat.label_author','Autore'),
        ('es','mat.label_author','Autor'),
        ('de','mat.label_author','Autor'),
        ('en','mat.author_placeholder','Full name of the content author'),
        ('it','mat.author_placeholder','Nome completo dell''autore del contenuto'),
        ('es','mat.author_placeholder','Nombre completo del autor del contenido'),
        ('de','mat.author_placeholder','Vollständiger Name des Inhaltsautors'),
        ('en','mat.label_owner','Responsible (system)'),
        ('it','mat.label_owner','Responsabile (sistema)'),
        ('es','mat.label_owner','Responsable (sistema)'),
        ('de','mat.label_owner','Verantwortlicher (System)'),
        ('en','mat.owner_hint','Person responsible for managing this material in the system.'),
        ('it','mat.owner_hint','Persona responsabile della gestione di questo materiale nel sistema.'),
        ('es','mat.owner_hint','Persona responsable de gestionar este material en el sistema.'),
        ('de','mat.owner_hint','Person, die für die Verwaltung dieses Materials im System zuständig ist.'),
        ('en','mat.label_folder','Folder'),
        ('it','mat.label_folder','Cartella'),
        ('es','mat.label_folder','Carpeta'),
        ('de','mat.label_folder','Ordner'),
        ('en','mat.folder_hint','Archive folder for verified materials.'),
        ('it','mat.folder_hint','Cartella di archiviazione per i materiali verificati.'),
        ('es','mat.folder_hint','Carpeta de archivo para materiales verificados.'),
        ('de','mat.folder_hint','Archivordner für verifizierte Materialien.'),
        ('en','mat.verified_fields','Verified fields'),
        ('it','mat.verified_fields','Campi verifica'),
        ('es','mat.verified_fields','Campos de verificación'),
        ('de','mat.verified_fields','Felder Verifikation'),
        ('en','mat.owner_search_placeholder','Search owner...'),
        ('it','mat.owner_search_placeholder','Cerca responsabile...'),
        ('es','mat.owner_search_placeholder','Buscar responsable...'),
        ('de','mat.owner_search_placeholder','Verantwortlichen suchen...'),
        ('en','mat.author_hint','Will be extracted automatically from document metadata if left blank.'),
        ('it','mat.author_hint','Verrà estratto automaticamente dai metadati del documento se lasciato vuoto.'),
        ('es','mat.author_hint','Se extraerá automáticamente de los metadatos del documento si se deja en blanco.'),
        ('de','mat.author_hint','Wird automatisch aus den Dokumentmetadaten extrahiert, wenn leer gelassen.');", conn);
    await ins.ExecuteNonQueryAsync();
}
catch { }

// ── Seed role hint simple key ──────────────────────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        ('en','admin.role_hint_simple','The Admin role is reserved.'),
        ('it','admin.role_hint_simple','Il ruolo Admin è riservato.'),
        ('es','admin.role_hint_simple','El rol Admin está reservado.'),
        ('de','admin.role_hint_simple','Die Admin-Rolle ist reserviert.');", conn);
    await ins.ExecuteNonQueryAsync();
}
catch { }

// ── Seed DocumentTypes translation keys ───────────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        ('en','doctype.none','No types defined.'),
        ('en','doctype.add','Add type'),
        ('en','doctype.name_placeholder','Document type name…'),
        ('en','doctype.create','Create type'),
        ('en','doctype.delete_confirm','Delete type'),
        ('en','common.actions','Actions'),
        ('it','doctype.none','Nessun tipo presente.'),
        ('it','doctype.add','Aggiungi tipo'),
        ('it','doctype.name_placeholder','Nome tipo documento…'),
        ('it','doctype.create','Crea tipo'),
        ('it','doctype.delete_confirm','Eliminare il tipo'),
        ('it','common.actions','Azioni');", conn);
    await ins.ExecuteNonQueryAsync();
    foreach (var lang in new[] { "es", "de" })
    {
        using var copy = new MySqlConnector.MySqlCommand(@"
            INSERT IGNORE INTO translations (language_code, label_key, label_value)
            SELECT @lang, label_key, label_value FROM translations
            WHERE language_code = 'en'
              AND label_key IN (
                'doctype.none','doctype.add','doctype.name_placeholder',
                'doctype.create','doctype.delete_confirm','common.actions');", conn);
        copy.Parameters.AddWithValue("@lang", lang);
        await copy.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Seed courses module prefix keys (split from old HTML-bearing keys) ────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        ('en','admin.courses_module_on_prefix','When the courses module is'),
        ('en','admin.courses_module_off_prefix','When the module is'),
        ('it','admin.courses_module_on_prefix','Quando il modulo corsi è'),
        ('it','admin.courses_module_off_prefix','Quando il modulo è');", conn);
    await ins.ExecuteNonQueryAsync();
    foreach (var lang in new[] { "es", "de" })
    {
        using var copy = new MySqlConnector.MySqlCommand(@"
            INSERT IGNORE INTO translations (language_code, label_key, label_value)
            SELECT @lang, label_key, label_value FROM translations
            WHERE language_code = 'en'
              AND label_key IN ('admin.courses_module_on_prefix','admin.courses_module_off_prefix');", conn);
        copy.Parameters.AddWithValue("@lang", lang);
        await copy.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Seed PlatformFeatures translation keys ────────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        ('en','common.manage','Manage'),
        ('en','common.back_to_panel','Back to panel'),
        ('en','admin.courses_module','Courses & Enrolments Module'),
        ('en','admin.module_active','Active'),
        ('en','admin.module_disabled','Disabled'),
        ('en','admin.modules_summary','Module summary'),
        ('en','admin.always_active','Always active'),
        ('en','admin.enable_courses','Enable Courses module'),
        ('en','admin.disable_courses','Disable Courses module'),
        ('en','admin.disable_courses_confirm','Disable the Courses module? Students and teachers will access the Materials library directly.'),
        ('it','common.manage','Gestisci'),
        ('it','common.back_to_panel','Torna al pannello'),
        ('it','admin.courses_module','Modulo Corsi e Iscrizioni'),
        ('it','admin.module_active','Attivo'),
        ('it','admin.module_disabled','Disabilitato'),
        ('it','admin.modules_summary','Riepilogo Moduli'),
        ('it','admin.always_active','Sempre attivo'),
        ('it','admin.enable_courses','Abilita modulo Corsi'),
        ('it','admin.disable_courses','Disabilita modulo Corsi'),
        ('it','admin.disable_courses_confirm','Disabilitare il modulo Corsi? Studenti e docenti accederanno direttamente alla libreria Materiali.'),
        ('en','admin.materials_module','Materials Module'),
        ('it','admin.materials_module','Modulo Materiali'),
        ('es','admin.materials_module','Módulo de Materiales'),
        ('de','admin.materials_module','Materialmodul'),
        ('en','admin.materials_module_on_prefix','When the materials module is'),
        ('it','admin.materials_module_on_prefix','Quando il modulo Materiali è'),
        ('es','admin.materials_module_on_prefix','Cuando el módulo de materiales está'),
        ('de','admin.materials_module_on_prefix','Wenn das Materialmodul'),
        ('en','admin.materials_feature_nav','The Materials link appears in the navigation bar'),
        ('it','admin.materials_feature_nav','Il link Materiali appare nella barra di navigazione'),
        ('es','admin.materials_feature_nav','El enlace Materiales aparece en la barra de navegación'),
        ('de','admin.materials_feature_nav','Der Materialien-Link erscheint in der Navigationsleiste'),
        ('en','admin.materials_feature_library','Users can browse, search and download materials'),
        ('it','admin.materials_feature_library','Gli utenti possono sfogliare, cercare e scaricare i materiali'),
        ('es','admin.materials_feature_library','Los usuarios pueden explorar, buscar y descargar materiales'),
        ('de','admin.materials_feature_library','Benutzer können Materialien durchsuchen und herunterladen'),
        ('en','admin.materials_feature_teacher','Teachers and admins can upload and manage materials'),
        ('it','admin.materials_feature_teacher','Docenti e admin possono caricare e gestire i materiali'),
        ('es','admin.materials_feature_teacher','Los docentes y admins pueden cargar y gestionar materiales'),
        ('de','admin.materials_feature_teacher','Lehrer und Admins können Materialien hochladen und verwalten'),
        ('en','admin.materials_module_info','Changes take effect immediately. Existing material data is not deleted.'),
        ('it','admin.materials_module_info','Le modifiche hanno effetto immediato. I dati dei materiali esistenti non vengono eliminati.'),
        ('es','admin.materials_module_info','Los cambios surten efecto de inmediato. Los materiales existentes no se eliminan.'),
        ('de','admin.materials_module_info','Änderungen wirken sofort. Vorhandene Materialdaten werden nicht gelöscht.'),
        ('en','home.no_modules_title','No active modules'),
        ('it','home.no_modules_title','Nessun modulo attivo'),
        ('es','home.no_modules_title','No hay módulos activos'),
        ('de','home.no_modules_title','Keine aktiven Module'),
        ('en','home.no_modules_desc','The platform has no enabled modules at the moment. Please contact the administrator.'),
        ('it','home.no_modules_desc','La piattaforma non ha moduli abilitati al momento. Contatta l\'amministratore.'),
        ('es','home.no_modules_desc','La plataforma no tiene módulos habilitados en este momento. Contacta al administrador.'),
        ('de','home.no_modules_desc','Die Plattform hat derzeit keine aktivierten Module. Bitte wenden Sie sich an den Administrator.'),
        ('en','auth.reset_landing_title','Reset password'),
        ('it','auth.reset_landing_title','Reimposta password'),
        ('es','auth.reset_landing_title','Restablecer contraseña'),
        ('de','auth.reset_landing_title','Passwort zurücksetzen'),
        ('en','auth.reset_landing_heading','Reset link confirmed'),
        ('it','auth.reset_landing_heading','Link di reset confermato'),
        ('es','auth.reset_landing_heading','Enlace de restablecimiento confirmado'),
        ('de','auth.reset_landing_heading','Reset-Link bestätigt'),
        ('en','auth.reset_landing_desc','Click the button to choose a new password. The link is valid for 1 hour.'),
        ('it','auth.reset_landing_desc','Clicca il pulsante per scegliere una nuova password. Il link è valido per 1 ora.'),
        ('es','auth.reset_landing_desc','Haz clic en el botón para elegir una nueva contraseña. El enlace es válido por 1 hora.'),
        ('de','auth.reset_landing_desc','Klicken Sie auf die Schaltfläche, um ein neues Passwort zu wählen. Der Link ist 1 Stunde gültig.'),
        ('en','auth.reset_landing_btn','Proceed to reset'),
        ('it','auth.reset_landing_btn','Procedi al reset'),
        ('es','auth.reset_landing_btn','Proceder al restablecimiento'),
        ('de','auth.reset_landing_btn','Zum Zurücksetzen fortfahren'),
        ('en','auth.cancel_go_login','Cancel and go to login'),
        ('it','auth.cancel_go_login','Annulla e torna al login'),
        ('es','auth.cancel_go_login','Cancelar y volver al inicio de sesión'),
        ('de','auth.cancel_go_login','Abbrechen und zum Login zurückkehren'),
        ('en','auth.pending_role_title','Account pending approval'),
        ('it','auth.pending_role_title','Account in attesa di approvazione'),
        ('es','auth.pending_role_title','Cuenta pendiente de aprobación'),
        ('de','auth.pending_role_title','Konto wartet auf Genehmigung'),
        ('en','auth.pending_role_heading','Registration complete!'),
        ('it','auth.pending_role_heading','Registrazione completata!'),
        ('es','auth.pending_role_heading','¡Registro completado!'),
        ('de','auth.pending_role_heading','Registrierung abgeschlossen!'),
        ('en','auth.pending_role_desc','Your account has been created successfully but no role has been assigned yet. An administrator will activate you shortly.'),
        ('it','auth.pending_role_desc','Il tuo account è stato creato con successo ma non è ancora stato assegnato un ruolo. Un amministratore ti attiverà a breve.'),
        ('es','auth.pending_role_desc','Tu cuenta se ha creado correctamente pero aún no se le ha asignado ningún rol. Un administrador te activará en breve.'),
        ('de','auth.pending_role_desc','Ihr Konto wurde erfolgreich erstellt, aber es wurde noch keine Rolle zugewiesen. Ein Administrator wird Sie in Kürze aktivieren.'),
        ('en','auth.pending_role_contact','If you need assistance, please contact platform support.'),
        ('it','auth.pending_role_contact','Se hai bisogno di assistenza, contatta il supporto della piattaforma.'),
        ('es','auth.pending_role_contact','Si necesitas asistencia, contacta con el soporte de la plataforma.'),
        ('de','auth.pending_role_contact','Wenn Sie Hilfe benötigen, wenden Sie sich an den Plattform-Support.'),
        ('en','admin.no_role','No role'),
        ('it','admin.no_role','Nessun ruolo'),
        ('es','admin.no_role','Sin rol'),
        ('de','admin.no_role','Keine Rolle'),
        ('en','admin.no_role_warning','This user has no role yet. Select a role and save.'),
        ('it','admin.no_role_warning','Questo utente non ha ancora un ruolo assegnato. Seleziona un ruolo e salva.'),
        ('es','admin.no_role_warning','Este usuario aún no tiene un rol asignado. Selecciona un rol y guarda.'),
        ('de','admin.no_role_warning','Diesem Benutzer ist noch keine Rolle zugewiesen. Wählen Sie eine Rolle aus und speichern Sie.'),
        ('en','admin.select_role','Select a role'),
        ('it','admin.select_role','Seleziona un ruolo'),
        ('es','admin.select_role','Seleccionar un rol'),
        ('de','admin.select_role','Eine Rolle auswählen'),
        ('en','users.delete_warning','This action is permanent and cannot be undone. All data associated with this user will also be deleted.'),
        ('it','users.delete_warning','Questa operazione è permanente e irreversibile. Tutti i dati associati a questo utente verranno eliminati definitivamente.'),
        ('es','users.delete_warning','Esta acción es permanente e irreversible. Todos los datos asociados a este usuario también serán eliminados.'),
        ('de','users.delete_warning','Diese Aktion ist dauerhaft und kann nicht rückgängig gemacht werden. Alle mit diesem Benutzer verknüpften Daten werden ebenfalls gelöscht.'),
        ('en','users.click_to_toggle','Click to toggle status'),
        ('it','users.click_to_toggle','Clicca per cambiare stato'),
        ('es','users.click_to_toggle','Haz clic para cambiar el estado'),
        ('de','users.click_to_toggle','Klicken, um den Status zu ändern'),
        ('en','nav.change_password','Change password'),
        ('it','nav.change_password','Cambia password'),
        ('es','nav.change_password','Cambiar contraseña'),
        ('de','nav.change_password','Passwort ändern');", conn);
    await ins.ExecuteNonQueryAsync();

    // ── UPDATE home.tagline (INSERT IGNORE non sovrascrive valori esistenti) ──
    using (var upd = new MySqlConnector.MySqlCommand(@"
        INSERT INTO translations (language_code, label_key, label_value) VALUES
            ('en','home.tagline','Teaching materials and courses platform for Bocconi University'),
            ('it','home.tagline','Piattaforma di gestione materiali didattici e corsi di Università Bocconi'),
            ('es','home.tagline','Plataforma de gestión de materiales didácticos y cursos para la Universidad Bocconi'),
            ('de','home.tagline','Plattform für Lehrmaterialien und Kurse der Universität Bocconi')
        ON DUPLICATE KEY UPDATE label_value = VALUES(label_value);", conn))
    { await upd.ExecuteNonQueryAsync(); }

    foreach (var lang in new[] { "es", "de" })
    {
        using var copy = new MySqlConnector.MySqlCommand(@"
            INSERT IGNORE INTO translations (language_code, label_key, label_value)
            SELECT @lang, label_key, label_value FROM translations
            WHERE language_code = 'en'
              AND label_key IN (
                'common.manage','common.back_to_panel','admin.courses_module','admin.module_active',
                'admin.module_disabled','admin.modules_summary','admin.always_active',
                'admin.enable_courses','admin.disable_courses','admin.disable_courses_confirm');", conn);
        copy.Parameters.AddWithValue("@lang", lang);
        await copy.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Seed admin.courses_label + fix admin.courses → "Courses" ─────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var cmd = new MySqlConnector.MySqlCommand(@"
        INSERT INTO translations (language_code, label_key, label_value) VALUES
            ('en','admin.courses_label','Courses'),
            ('it','admin.courses_label','Corsi'),
            ('es','admin.courses_label','Cursos'),
            ('de','admin.courses_label','Kurse'),
            ('en','admin.courses','Courses'),
            ('it','admin.courses','Corsi'),
            ('es','admin.courses','Cursos'),
            ('de','admin.courses','Kurse')
        ON DUPLICATE KEY UPDATE label_value = VALUES(label_value);", conn);
    await cmd.ExecuteNonQueryAsync();
}
catch { }

// ── Seed Admin Users/Roles translation keys ───────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        ('en','admin.users_tab','Users'),
        ('en','admin.roles_tab','Roles'),
        ('en','admin.add_role','Add new role'),
        ('en','admin.role_name_placeholder','Role name (e.g. Tutor, Supervisor…)'),
        ('en','admin.create_role','Create role'),
        ('en','admin.role_hint','Only letters, numbers, underscores and spaces. The Admin role is reserved.'),
        ('en','admin.role_protected','protected'),
        ('en','admin.edit_role','Edit role'),
        ('en','admin.delete_role_blocked','Cannot delete: users have this role'),
        ('en','admin.delete_role','Delete role'),
        ('en','admin.delete_role_confirm','Delete role'),
        ('it','admin.users_tab','Utenti'),
        ('it','admin.roles_tab','Ruoli'),
        ('it','admin.add_role','Aggiungi nuovo ruolo'),
        ('it','admin.role_name_placeholder','Nome ruolo (es. Tutor, Supervisore…)'),
        ('it','admin.create_role','Crea ruolo'),
        ('it','admin.role_hint','Solo lettere, numeri, underscore e spazi. Il ruolo Admin è riservato.'),
        ('it','admin.role_protected','protetto'),
        ('it','admin.edit_role','Modifica ruolo'),
        ('it','admin.delete_role_blocked','Impossibile eliminare: utenti hanno questo ruolo'),
        ('it','admin.delete_role','Elimina ruolo'),
        ('it','admin.delete_role_confirm','Eliminare il ruolo');", conn);
    await ins.ExecuteNonQueryAsync();
    foreach (var lang in new[] { "es", "de" })
    {
        using var copy = new MySqlConnector.MySqlCommand(@"
            INSERT IGNORE INTO translations (language_code, label_key, label_value)
            SELECT @lang, label_key, label_value FROM translations
            WHERE language_code = 'en'
              AND label_key IN (
                'admin.users_tab','admin.roles_tab','admin.add_role',
                'admin.role_name_placeholder','admin.create_role','admin.role_hint',
                'admin.role_protected','admin.edit_role','admin.delete_role_blocked',
                'admin.delete_role','admin.delete_role_confirm');", conn);
        copy.Parameters.AddWithValue("@lang", lang);
        await copy.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Migrate materials table: add status + protocol_number columns ─────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var colCheck = new MySqlConnector.MySqlCommand(
        "SELECT COUNT(*) FROM information_schema.columns " +
        "WHERE table_schema=DATABASE() AND table_name='materials' AND column_name='status';", conn);
    var colExists = Convert.ToInt32(await colCheck.ExecuteScalarAsync()) > 0;
    if (!colExists)
    {
        using var alter = new MySqlConnector.MySqlCommand(@"
            ALTER TABLE materials
                ADD COLUMN status VARCHAR(20) NOT NULL DEFAULT 'bozza',
                ADD COLUMN protocol_number VARCHAR(50) NULL;", conn);
        await alter.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Seed mat.status_* + mat.protocol_number translation keys ─────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        ('en','mat.label_status','Status'),
        ('en','mat.col_status','Status'),
        ('en','mat.status_bozza','Draft'),
        ('en','mat.status_in_revisione','In review'),
        ('en','mat.status_verificato','Verified'),
        ('en','mat.select_status','— Select status —'),
        ('en','mat.protocol_number','Protocol number'),
        ('en','mat.protocol_auto','Assigned automatically on verification'),
        ('en','mat.col_protocol','Protocol'),
        ('it','mat.label_status','Stato'),
        ('it','mat.col_status','Stato'),
        ('it','mat.status_bozza','Bozza'),
        ('it','mat.status_in_revisione','In revisione'),
        ('it','mat.status_verificato','Verificato'),
        ('it','mat.select_status','— Seleziona stato —'),
        ('it','mat.protocol_number','Numero di protocollo'),
        ('it','mat.protocol_auto','Assegnato automaticamente alla verifica'),
        ('it','mat.col_protocol','Protocollo');", conn);
    await ins.ExecuteNonQueryAsync();
    foreach (var lang in new[] { "es", "de" })
    {
        using var copy = new MySqlConnector.MySqlCommand(@"
            INSERT IGNORE INTO translations (language_code, label_key, label_value)
            SELECT @lang, label_key, label_value FROM translations
            WHERE language_code='en' AND label_key IN (
                'mat.label_status','mat.col_status','mat.status_bozza',
                'mat.status_in_revisione','mat.status_verificato','mat.select_status',
                'mat.protocol_number','mat.protocol_auto','mat.col_protocol');", conn);
        copy.Parameters.AddWithValue("@lang", lang);
        await copy.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Materials: add area_id and catalogation_date columns ────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();

    using var chkArea = new MySqlConnector.MySqlCommand(@"
        SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'materials' AND COLUMN_NAME = 'area_id';", conn);
    if (Convert.ToInt32(await chkArea.ExecuteScalarAsync()) == 0)
    {
        using var addArea = new MySqlConnector.MySqlCommand(
            "ALTER TABLE materials ADD COLUMN area_id INT NULL, ADD CONSTRAINT fk_mat_area FOREIGN KEY (area_id) REFERENCES areas(id) ON DELETE SET NULL;", conn);
        await addArea.ExecuteNonQueryAsync();
    }

    using var chkCat = new MySqlConnector.MySqlCommand(@"
        SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'materials' AND COLUMN_NAME = 'catalogation_date';", conn);
    if (Convert.ToInt32(await chkCat.ExecuteScalarAsync()) == 0)
    {
        using var addCat = new MySqlConnector.MySqlCommand(
            "ALTER TABLE materials ADD COLUMN catalogation_date DATETIME NULL;", conn);
        await addCat.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Seed mat area/catalogation + menu permission + nav labels ─────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        -- Materials: Area and catalogation date labels
        ('en','mat.label_area','Area'),
        ('it','mat.label_area','Area'),
        ('es','mat.label_area','Área'),
        ('de','mat.label_area','Bereich'),
        ('en','mat.select_area','— No area —'),
        ('it','mat.select_area','— Nessuna area —'),
        ('es','mat.select_area','— Sin área —'),
        ('de','mat.select_area','— Kein Bereich —'),
        ('en','mat.label_cat_date','Catalogation date'),
        ('it','mat.label_cat_date','Data catalogazione'),
        ('es','mat.label_cat_date','Fecha de catalogación'),
        ('de','mat.label_cat_date','Katalogisierungsdatum'),
        -- Navbar menu items (Users / Translations)
        ('en','nav.users','Users'),
        ('it','nav.users','Utenti'),
        ('es','nav.users','Usuarios'),
        ('de','nav.users','Benutzer'),
        ('en','nav.translations','Translations'),
        ('it','nav.translations','Traduzioni'),
        ('es','nav.translations','Traducciones'),
        ('de','nav.translations','Übersetzungen'),
        -- Admin dashboard
        ('en','admin.dashboard','Administration'),
        ('it','admin.dashboard','Amministrazione'),
        ('es','admin.dashboard','Administración'),
        ('de','admin.dashboard','Verwaltung'),
        ('en','admin.platform_settings_desc','Platform configuration and feature toggles'),
        ('it','admin.platform_settings_desc','Configurazione della piattaforma e abilitazione funzionalità'),
        ('es','admin.platform_settings_desc','Configuración de la plataforma y activación de funciones'),
        ('de','admin.platform_settings_desc','Plattformkonfiguration und Funktionsschalter'),
        ('en','admin.email_settings','Email Settings'),
        ('it','admin.email_settings','Impostazioni Email'),
        ('es','admin.email_settings','Configuración de correo'),
        ('de','admin.email_settings','E-Mail-Einstellungen'),
        ('en','admin.email_settings_desc','Configure SMTP server and send test emails'),
        ('it','admin.email_settings_desc','Configura il server SMTP e invia email di test'),
        ('es','admin.email_settings_desc','Configurar servidor SMTP y enviar correos de prueba'),
        ('de','admin.email_settings_desc','SMTP-Server konfigurieren und Test-E-Mails senden'),
        ('en','admin.configure','Configure'),
        ('it','admin.configure','Configura'),
        ('es','admin.configure','Configurar'),
        ('de','admin.configure','Konfigurieren'),
        ('en','admin.configure_email','Configure email'),
        ('it','admin.configure_email','Configura email'),
        ('es','admin.configure_email','Configurar correo'),
        ('de','admin.configure_email','E-Mail konfigurieren'),
        -- Menu permission labels (EditRole page)
        ('en','perm.menu_access','Menu Access'),
        ('it','perm.menu_access','Accesso Menu'),
        ('es','perm.menu_access','Acceso al Menú'),
        ('de','perm.menu_access','Menü-Zugang'),
        ('en','perm.menu_access_hint','Allows users with this role to access the following panel sections.'),
        ('it','perm.menu_access_hint','Consente agli utenti con questo ruolo di accedere alle seguenti sezioni del pannello.'),
        ('es','perm.menu_access_hint','Permite a los usuarios con este rol acceder a las siguientes secciones del panel.'),
        ('de','perm.menu_access_hint','Ermöglicht Benutzern mit dieser Rolle den Zugriff auf folgende Panelbereiche.'),
        ('en','perm.menu_users','Users — section access'),
        ('it','perm.menu_users','Utenti — accesso sezione'),
        ('es','perm.menu_users','Usuarios — acceso a sección'),
        ('de','perm.menu_users','Benutzer — Bereichszugang'),
        ('en','perm.menu_translations','Dictionary — section access'),
        ('it','perm.menu_translations','Dictionary — accesso sezione'),
        ('es','perm.menu_translations','Dictionary — acceso a sección'),
        ('de','perm.menu_translations','Dictionary — Bereichszugang'),
        ('en','perm.menu_materials','Materials — section access'),
        ('it','perm.menu_materials','Materiali — accesso sezione'),
        ('es','perm.menu_materials','Materiales — acceso a sección'),
        ('de','perm.menu_materials','Materialien — Bereichszugang'),
        ('en','perm.mat_ops_hint','Allowed operations on materials:'),
        ('it','perm.mat_ops_hint','Operazioni consentite sui materiali:'),
        ('es','perm.mat_ops_hint','Operaciones permitidas en materiales:'),
        ('de','perm.mat_ops_hint','Erlaubte Vorgänge für Materialien:'),
        ('en','perm.materials_create_setstatus','Can change status when creating'),
        ('it','perm.materials_create_setstatus','Può modificare lo stato in creazione'),
        ('es','perm.materials_create_setstatus','Puede cambiar el estado al crear'),
        ('de','perm.materials_create_setstatus','Status beim Erstellen ändern'),
        ('en','perm.materials_edit_setstatus','Can change status when editing'),
        ('it','perm.materials_edit_setstatus','Può modificare lo stato in modifica'),
        ('es','perm.materials_edit_setstatus','Puede cambiar el estado al editar'),
        ('de','perm.materials_edit_setstatus','Status beim Bearbeiten ändern'),
        ('en','perm.materials_approve_setstatus','Can change status when approving'),
        ('it','perm.materials_approve_setstatus','Può modificare lo stato in approvazione'),
        ('es','perm.materials_approve_setstatus','Puede cambiar el estado al aprobar'),
        ('de','perm.materials_approve_setstatus','Status beim Genehmigen ändern'),
        ('en','mat.status_locked_create','Status is locked to Draft for your role.'),
        ('it','mat.status_locked_create','Lo stato è bloccato su Bozza per il tuo ruolo.'),
        ('es','mat.status_locked_create','El estado está bloqueado en Borrador para tu rol.'),
        ('de','mat.status_locked_create','Status ist für Ihre Rolle auf Entwurf gesperrt.'),
        ('en','mat.status_locked_edit','Status cannot be changed with your role.'),
        ('it','mat.status_locked_edit','Lo stato non è modificabile con il tuo ruolo.'),
        ('es','mat.status_locked_edit','No puedes cambiar el estado con tu rol.'),
        ('de','mat.status_locked_edit','Status kann mit Ihrer Rolle nicht geändert werden.'),
        ('en','perm.materials_setstatus_all','Allow status change'),
        ('it','perm.materials_setstatus_all','Consenti modifica stato'),
        ('es','perm.materials_setstatus_all','Permitir cambio de estado'),
        ('de','perm.materials_setstatus_all','Statusänderung erlauben'),
        ('en','perm.materials_setstatus_all_hint','Bypasses the automatic lock (draft on create, in-review on edit)'),
        ('it','perm.materials_setstatus_all_hint','Bypassa il blocco automatico (bozza in creazione, in revisione in modifica)'),
        ('es','perm.materials_setstatus_all_hint','Evita el bloqueo automático (borrador al crear, en revisión al editar)'),
        ('de','perm.materials_setstatus_all_hint','Umgeht die automatische Sperre (Entwurf beim Erstellen, In Überprüfung beim Bearbeiten)'),
        -- Email settings: password hints
        ('en','admin.email.password_set','Password already set — leave the field blank to keep it unchanged.'),
        ('it','admin.email.password_set','Password già impostata — lascia il campo vuoto per mantenerla invariata.'),
        ('es','admin.email.password_set','Contraseña ya establecida — deja el campo vacío para mantenerla.'),
        ('de','admin.email.password_set','Passwort bereits gesetzt — Feld leer lassen, um es beizubehalten.'),
        ('en','admin.email.password_keep','Leave blank to keep unchanged'),
        ('it','admin.email.password_keep','Lascia vuoto per mantenerla invariata'),
        ('es','admin.email.password_keep','Dejar vacío para mantener sin cambios'),
        ('de','admin.email.password_keep','Leer lassen, um unverändert zu behalten'),
        ('en','admin.email.password_empty','Enter the SMTP password.'),
        ('it','admin.email.password_empty','Inserisci la password SMTP.'),
        ('es','admin.email.password_empty','Introduce la contraseña SMTP.'),
        ('de','admin.email.password_empty','Geben Sie das SMTP-Passwort ein.'),
        -- Error page
        ('en','error.page_title','Error'),
        ('it','error.page_title','Errore'),
        ('es','error.page_title','Error'),
        ('de','error.page_title','Fehler'),
        ('en','error.heading','An error has occurred'),
        ('it','error.heading','Si è verificato un errore'),
        ('es','error.heading','Se ha producido un error'),
        ('de','error.heading','Ein Fehler ist aufgetreten'),
        ('en','error.message','Please try again or contact technical support.'),
        ('it','error.message','Riprova o contatta il supporto tecnico.'),
        ('es','error.message','Inténtalo de nuevo o contacta con el soporte técnico.'),
        ('de','error.message','Bitte versuchen Sie es erneut oder wenden Sie sich an den technischen Support.'),
        ('en','error.back_home','Back to home'),
        ('it','error.back_home','Torna alla home'),
        ('es','error.back_home','Volver al inicio'),
        ('de','error.back_home','Zurück zur Startseite');", conn);
    await ins.ExecuteNonQueryAsync();
}
catch { }

// ── Migrate: ruoli con permessi materials.* ricevono automaticamente menu.materials ──
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var mig = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO role_permissions (role_id, permission_key)
        SELECT DISTINCT role_id, 'menu.materials'
        FROM role_permissions
        WHERE permission_key IN ('materials.create','materials.edit','materials.approve')", conn);
    await mig.ExecuteNonQueryAsync();
}
catch { }


// ── Update perm.menu_translations label to "Dictionary" ───────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var upd = new MySqlConnector.MySqlCommand(@"
        UPDATE translations SET label_value = CASE language_code
            WHEN 'en' THEN 'Dictionary — section access'
            WHEN 'it' THEN 'Dictionary — accesso sezione'
            WHEN 'es' THEN 'Dictionary — acceso a sección'
            WHEN 'de' THEN 'Dictionary — Bereichszugang'
        END
        WHERE label_key = 'perm.menu_translations';", conn);
    await upd.ExecuteNonQueryAsync();
}
catch { }

// ── Seed nav.dictionary + common.cancel keys ──────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        ('en','nav.dictionary','Dictionary'),
        ('it','nav.dictionary','Dizionario'),
        ('es','nav.dictionary','Diccionario'),
        ('de','nav.dictionary','Wörterbuch'),
        ('en','nav.materials','Materials'),
        ('it','nav.materials','Materiali'),
        ('es','nav.materials','Materiales'),
        ('de','nav.materials','Materialien'),
        ('en','nav.users_label','Users'),
        ('it','nav.users_label','Utenti'),
        ('es','nav.users_label','Usuarios'),
        ('de','nav.users_label','Benutzer'),
        ('en','common.cancel','Cancel'),
        ('it','common.cancel','Annulla'),
        ('es','common.cancel','Cancelar'),
        ('de','common.cancel','Abbrechen');", conn);
    await ins.ExecuteNonQueryAsync();
}
catch { }

// ── Seed filtri Materials: anno catalogazione, anno modifica, cartella ────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        ('en','mat.filter_cat_year','Catalogation year'),
        ('it','mat.filter_cat_year','Anno catalogazione'),
        ('es','mat.filter_cat_year','Año catalogación'),
        ('de','mat.filter_cat_year','Katalogisierungsjahr'),
        ('en','mat.filter_mod_year','Modification year'),
        ('it','mat.filter_mod_year','Anno modifica'),
        ('es','mat.filter_mod_year','Año modificación'),
        ('de','mat.filter_mod_year','Änderungsjahr'),
        ('en','mat.filter_folder_name','Folder name'),
        ('it','mat.filter_folder_name','Nome cartella'),
        ('es','mat.filter_folder_name','Nombre carpeta'),
        ('de','mat.filter_folder_name','Ordnername'),
        ('en','mat.filter_folder_name_ph','Search by folder name…'),
        ('it','mat.filter_folder_name_ph','Cerca per nome cartella…'),
        ('es','mat.filter_folder_name_ph','Buscar por nombre de carpeta…'),
        ('de','mat.filter_folder_name_ph','Nach Ordnernamen suchen…'),
        ('en','mat.filter_folder_id','Folder ID'),
        ('it','mat.filter_folder_id','ID cartella'),
        ('es','mat.filter_folder_id','ID carpeta'),
        ('de','mat.filter_folder_id','Ordner-ID');", conn);
    await ins.ExecuteNonQueryAsync();
}
catch { }

// ── Fix mat.label_area / mat.select_area / mat.label_cat_date (force correct values) ──
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var upd = new MySqlConnector.MySqlCommand(@"
        INSERT INTO translations (language_code, label_key, label_value) VALUES
            ('en','mat.label_area','Area'),
            ('it','mat.label_area','Area'),
            ('es','mat.label_area','Área'),
            ('de','mat.label_area','Bereich'),
            ('en','mat.select_area','— No area —'),
            ('it','mat.select_area','— Nessuna area —'),
            ('es','mat.select_area','— Sin área —'),
            ('de','mat.select_area','— Kein Bereich —'),
            ('en','mat.label_cat_date','Catalogation date'),
            ('it','mat.label_cat_date','Data catalogazione'),
            ('es','mat.label_cat_date','Fecha de catalogación'),
            ('de','mat.label_cat_date','Katalogisierungsdatum'),
            ('en','mat.upload_optional','Upload new version file (optional)'),
            ('it','mat.upload_optional','Carica nuovo file di versione (opzionale)'),
            ('es','mat.upload_optional','Subir nuevo archivo de versión (opcional)'),
            ('de','mat.upload_optional','Neue Versionsdatei hochladen (optional)')
        ON DUPLICATE KEY UPDATE label_value = VALUES(label_value);", conn);
    await upd.ExecuteNonQueryAsync();
}
catch { }

// ── Ensure nav.users has correct per-language values (fix old Italian fallback) ──
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var upd = new MySqlConnector.MySqlCommand(@"
        INSERT INTO translations (language_code, label_key, label_value) VALUES
            ('en','nav.users','Users'),
            ('it','nav.users','Utenti'),
            ('es','nav.users','Usuarios'),
            ('de','nav.users','Benutzer')
        ON DUPLICATE KEY UPDATE label_value = VALUES(label_value);", conn);
    await upd.ExecuteNonQueryAsync();
}
catch { }

// ── Seed: email notification settings keys ───────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var upd = new MySqlConnector.MySqlCommand(@"
        INSERT INTO translations (language_code, label_key, label_value) VALUES
            ('en','admin.email.notify_material','Notify on material create/update'),
            ('en','admin.email.notify_material_desc','Send an email to selected roles when a material is created or modified.'),
            ('en','admin.email.notify_roles','Roles to notify'),
            ('it','admin.email.notify_material','Notifica creazione/modifica materiali'),
            ('it','admin.email.notify_material_desc','Invia un''email ai ruoli selezionati quando un materiale viene creato o modificato.'),
            ('it','admin.email.notify_roles','Ruoli da notificare'),
            ('es','admin.email.notify_material','Notificar creación/modificación de materiales'),
            ('es','admin.email.notify_material_desc','Envía un correo a los roles seleccionados cuando se crea o modifica un material.'),
            ('es','admin.email.notify_roles','Roles a notificar'),
            ('de','admin.email.notify_material','Benachrichtigung bei Materialerstellung/-änderung'),
            ('de','admin.email.notify_material_desc','Sendet eine E-Mail an die ausgewählten Rollen, wenn ein Material erstellt oder geändert wird.'),
            ('de','admin.email.notify_roles','Zu benachrichtigende Rollen')
        ON DUPLICATE KEY UPDATE label_value = VALUES(label_value);", conn);
    await upd.ExecuteNonQueryAsync();
}
catch { }

// ── Seed: admin email settings messages ──────────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var upd = new MySqlConnector.MySqlCommand(@"
        INSERT INTO translations (language_code, label_key, label_value) VALUES
            ('en','admin.email.saved','Email settings saved successfully.'),
            ('en','admin.email.save_error','Error saving settings: {0}'),
            ('en','admin.email.test_no_recipient','Enter an email address for the test.'),
            ('en','admin.email.test_sent','Test email sent to {0}.'),
            ('en','admin.email.test_failed','Send failed: {0}'),
            ('it','admin.email.saved','Impostazioni email salvate con successo.'),
            ('it','admin.email.save_error','Errore nel salvataggio: {0}'),
            ('it','admin.email.test_no_recipient','Inserire un indirizzo email per il test.'),
            ('it','admin.email.test_sent','Email di test inviata a {0}.'),
            ('it','admin.email.test_failed','Invio fallito: {0}'),
            ('es','admin.email.saved','Configuración de correo guardada correctamente.'),
            ('es','admin.email.save_error','Error al guardar: {0}'),
            ('es','admin.email.test_no_recipient','Introduce una dirección de correo para la prueba.'),
            ('es','admin.email.test_sent','Correo de prueba enviado a {0}.'),
            ('es','admin.email.test_failed','Envío fallido: {0}'),
            ('de','admin.email.saved','E-Mail-Einstellungen erfolgreich gespeichert.'),
            ('de','admin.email.save_error','Fehler beim Speichern: {0}'),
            ('de','admin.email.test_no_recipient','Bitte eine E-Mail-Adresse für den Test eingeben.'),
            ('de','admin.email.test_sent','Test-E-Mail an {0} gesendet.'),
            ('de','admin.email.test_failed','Senden fehlgeschlagen: {0}')
        ON DUPLICATE KEY UPDATE label_value = VALUES(label_value);", conn);
    await upd.ExecuteNonQueryAsync();
}
catch { }

// ── Fix: admin.role_updated translation key ───────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var upd = new MySqlConnector.MySqlCommand(@"
        INSERT INTO translations (language_code, label_key, label_value) VALUES
            ('en','admin.role_updated','Role updated to ''{0}''.'),
            ('it','admin.role_updated','Ruolo aggiornato in ''{0}''.'),
            ('es','admin.role_updated','Rol actualizado a ''{0}''.'),
            ('de','admin.role_updated','Rolle aktualisiert auf ''{0}''.')
        ON DUPLICATE KEY UPDATE label_value = VALUES(label_value);", conn);
    await upd.ExecuteNonQueryAsync();
}
catch { }

// ── Fix: admin.edit_role label (rename from "Edit role name" → "Edit role") ──
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var upd = new MySqlConnector.MySqlCommand(@"
        INSERT INTO translations (language_code, label_key, label_value) VALUES
            ('en','admin.edit_role','Edit role'),
            ('it','admin.edit_role','Modifica ruolo')
        ON DUPLICATE KEY UPDATE label_value = VALUES(label_value);", conn);
    await upd.ExecuteNonQueryAsync();
}
catch { }

// ── Seed bulk-download + delete-version translation keys ─────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        ('en','mat.selected','selected'),
        ('it','mat.selected','selezionati'),
        ('es','mat.selected','seleccionados'),
        ('de','mat.selected','ausgewählt'),
        ('en','mat.deselect_all','Deselect all'),
        ('it','mat.deselect_all','Deseleziona tutti'),
        ('es','mat.deselect_all','Deseleccionar todos'),
        ('de','mat.deselect_all','Alle abwählen'),
        ('en','mat.bulk_download','Download ZIP'),
        ('it','mat.bulk_download','Scarica ZIP'),
        ('es','mat.bulk_download','Descargar ZIP'),
        ('de','mat.bulk_download','ZIP herunterladen'),
        ('en','mat.delete_version_confirm','Delete version'),
        ('it','mat.delete_version_confirm','Eliminare la versione'),
        ('es','mat.delete_version_confirm','Eliminar versión'),
        ('de','mat.delete_version_confirm','Version löschen'),
        ('en','mat.delete_version_btn','Delete version'),
        ('it','mat.delete_version_btn','Elimina versione'),
        ('es','mat.delete_version_btn','Eliminar versión'),
        ('de','mat.delete_version_btn','Version löschen');", conn);
    await ins.ExecuteNonQueryAsync();
}
catch { }

// ── Drop legacy documents / document_versions tables (replaced by Materials Library) ─
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();

    // Drop document_versions first (FK child)
    using var chkDv = new MySqlConnector.MySqlCommand(@"
        SELECT COUNT(*) FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'document_versions';", conn);
    if (Convert.ToInt32(await chkDv.ExecuteScalarAsync()) > 0)
    {
        using var dropDv = new MySqlConnector.MySqlCommand(
            "DROP TABLE document_versions;", conn);
        await dropDv.ExecuteNonQueryAsync();
    }

    // Then drop documents
    using var chkD = new MySqlConnector.MySqlCommand(@"
        SELECT COUNT(*) FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'documents';", conn);
    if (Convert.ToInt32(await chkD.ExecuteScalarAsync()) > 0)
    {
        using var dropD = new MySqlConnector.MySqlCommand(
            "DROP TABLE documents;", conn);
        await dropD.ExecuteNonQueryAsync();
    }
}
catch { }

// ── Seed mat.* UI validation & upload keys ────────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
        ('it','mat.file_required_warn','Seleziona prima un file da caricare.'),
        ('en','mat.file_required_warn','Please select a file to upload.'),
        ('es','mat.file_required_warn','Por favor, selecciona un archivo para cargar.'),
        ('de','mat.file_required_warn','Bitte wähle zuerst eine Datei zum Hochladen aus.'),
        ('it','mat.title_required','Il titolo è obbligatorio'),
        ('en','mat.title_required','Title is required'),
        ('es','mat.title_required','El título es obligatorio'),
        ('de','mat.title_required','Titel ist erforderlich'),
        ('it','mat.doctype_required','Il tipo documento è obbligatorio'),
        ('en','mat.doctype_required','Document type is required'),
        ('es','mat.doctype_required','El tipo de documento es obligatorio'),
        ('de','mat.doctype_required','Dokumenttyp ist erforderlich'),
        ('it','mat.similar_titles_found','Attenzione: materiali con titolo simile già presenti'),
        ('en','mat.similar_titles_found','Warning: materials with a similar title already exist'),
        ('es','mat.similar_titles_found','Atención: ya existen materiales con un título similar'),
        ('de','mat.similar_titles_found','Achtung: Materialien mit ähnlichem Titel bereits vorhanden'),
        ('it','mat.upload_document','Carica Documento'),
        ('en','mat.upload_document','Upload Document'),
        ('es','mat.upload_document','Subir Documento'),
        ('de','mat.upload_document','Dokument hochladen'),
        ('it','mat.upload_hint','Clicca per selezionare un file dal tuo computer'),
        ('en','mat.upload_hint','Click to select a file from your computer'),
        ('es','mat.upload_hint','Haz clic para seleccionar un archivo de tu ordenador'),
        ('de','mat.upload_hint','Klicke, um eine Datei von deinem Computer auszuwählen'),
        ('it','mat.file_lost_warn','Il file non è stato salvato. Selezionalo di nuovo per procedere.'),
        ('en','mat.file_lost_warn','The file was not saved. Please select it again to continue.'),
        ('es','mat.file_lost_warn','El archivo no se guardó. Vuelve a seleccionarlo para continuar.'),
        ('de','mat.file_lost_warn','Die Datei wurde nicht gespeichert. Bitte wähle sie erneut aus, um fortzufahren.'),
        ('it','mat.remove_file','Rimuovi file'),
        ('en','mat.remove_file','Remove file'),
        ('es','mat.remove_file','Eliminar archivo'),
        ('de','mat.remove_file','Datei entfernen'),
        ('it','mat.verified_modal_title','Verifica completata'),
        ('en','mat.verified_modal_title','Verification complete'),
        ('es','mat.verified_modal_title','Verificación completada'),
        ('de','mat.verified_modal_title','Überprüfung abgeschlossen'),
        ('it','mat.verified_modal_hint','Completa i dati di registrazione prima di salvare come Verificato.'),
        ('en','mat.verified_modal_hint','Complete the registration data before saving as Verified.'),
        ('es','mat.verified_modal_hint','Completa los datos de registro antes de guardar como Verificado.'),
        ('de','mat.verified_modal_hint','Füllen Sie die Registrierungsdaten aus, bevor Sie als Verifiziert speichern.'),
        ('it','mat.folder_placeholder','Digita o seleziona una cartella…'),
        ('en','mat.folder_placeholder','Type or select a folder…'),
        ('es','mat.folder_placeholder','Escribe o selecciona una carpeta…'),
        ('de','mat.folder_placeholder','Ordner eingeben oder auswählen…'),
        ('it','mat.folder_required','Inserisci il nome della cartella.'),
        ('en','mat.folder_required','Please enter the folder name.'),
        ('es','mat.folder_required','Por favor, introduce el nombre de la carpeta.'),
        ('de','mat.folder_required','Bitte gib den Ordnernamen ein.'),
        ('it','mat.modal_confirm','Conferma e salva'),
        ('en','mat.modal_confirm','Confirm and save'),
        ('es','mat.modal_confirm','Confirmar y guardar'),
        ('de','mat.modal_confirm','Bestätigen und speichern'),
        ('it','mat.pages','pagine'),
        ('en','mat.pages','pages'),
        ('es','mat.pages','páginas'),
        ('de','mat.pages','Seiten'),
        ('it','mat.slides','slide'),
        ('en','mat.slides','slides'),
        ('es','mat.slides','diapositivas'),
        ('de','mat.slides','Folien');", conn);
    await ins.ExecuteNonQueryAsync();
}
catch { }

// ── Seed admin.email.* notification & UI keys ─────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT INTO translations (language_code, label_key, label_value) VALUES
        ('it','admin.email.page_title','Impostazioni Email'),
        ('en','admin.email.page_title','Email Settings'),
        ('es','admin.email.page_title','Configuración de correo'),
        ('de','admin.email.page_title','E-Mail-Einstellungen'),
        ('it','admin.email.smtp_config','Configurazione SMTP'),
        ('en','admin.email.smtp_config','SMTP Configuration'),
        ('es','admin.email.smtp_config','Configuración SMTP'),
        ('de','admin.email.smtp_config','SMTP-Konfiguration'),
        ('it','admin.email.host','Server SMTP'),
        ('en','admin.email.host','SMTP Host'),
        ('es','admin.email.host','Host SMTP'),
        ('de','admin.email.host','SMTP-Host'),
        ('it','admin.email.port','Porta'),
        ('en','admin.email.port','Port'),
        ('es','admin.email.port','Puerto'),
        ('de','admin.email.port','Port'),
        ('it','admin.email.username','Nome utente'),
        ('en','admin.email.username','Username'),
        ('es','admin.email.username','Nombre de usuario'),
        ('de','admin.email.username','Benutzername'),
        ('it','admin.email.password','Password'),
        ('en','admin.email.password','Password'),
        ('es','admin.email.password','Contraseña'),
        ('de','admin.email.password','Passwort'),
        ('it','admin.email.from_email','Email mittente'),
        ('en','admin.email.from_email','Sender Email'),
        ('es','admin.email.from_email','Correo del remitente'),
        ('de','admin.email.from_email','Absender-E-Mail'),
        ('it','admin.email.from_name','Nome mittente'),
        ('en','admin.email.from_name','Sender Name'),
        ('es','admin.email.from_name','Nombre del remitente'),
        ('de','admin.email.from_name','Absendername'),
        ('it','admin.email.use_ssl','Usa SSL (porta 465)'),
        ('en','admin.email.use_ssl','Use SSL (port 465)'),
        ('es','admin.email.use_ssl','Usar SSL (puerto 465)'),
        ('de','admin.email.use_ssl','SSL verwenden (Port 465)'),
        ('it','admin.email.use_ssl_desc','Disabilita per usare STARTTLS (porta 587).'),
        ('en','admin.email.use_ssl_desc','Disable to use STARTTLS (port 587).'),
        ('es','admin.email.use_ssl_desc','Desactiva para usar STARTTLS (puerto 587).'),
        ('de','admin.email.use_ssl_desc','Deaktivieren, um STARTTLS (Port 587) zu verwenden.'),
        ('it','admin.email.save_btn','Salva impostazioni'),
        ('en','admin.email.save_btn','Save Settings'),
        ('es','admin.email.save_btn','Guardar configuración'),
        ('de','admin.email.save_btn','Einstellungen speichern'),
        ('it','admin.email.enable','Abilita invio email'),
        ('en','admin.email.enable','Enable email sending'),
        ('es','admin.email.enable','Habilitar envío de correos'),
        ('de','admin.email.enable','E-Mail-Versand aktivieren'),
        ('it','admin.email.enable_desc','Se disabilitato, le email vengono registrate ma non inviate.'),
        ('en','admin.email.enable_desc','If disabled, emails are logged but not sent.'),
        ('es','admin.email.enable_desc','Si está desactivado, los correos se registran pero no se envían.'),
        ('de','admin.email.enable_desc','Wenn deaktiviert, werden E-Mails protokolliert, aber nicht gesendet.'),
        ('it','admin.email.test_btn','Invia email di test'),
        ('en','admin.email.test_btn','Send Test Email'),
        ('es','admin.email.test_btn','Enviar correo de prueba'),
        ('de','admin.email.test_btn','Test-E-Mail senden'),
        ('it','admin.email.test_not_configured','Salva prima la configurazione SMTP per abilitare il test.'),
        ('en','admin.email.test_not_configured','Save the SMTP configuration first to enable the test.'),
        ('es','admin.email.test_not_configured','Guarda primero la configuración SMTP para habilitar el test.'),
        ('de','admin.email.test_not_configured','Speichere zuerst die SMTP-Konfiguration, um den Test zu aktivieren.'),
        ('it','admin.email.notify_materials_header','Notifiche materiali'),
        ('en','admin.email.notify_materials_header','Material notifications'),
        ('es','admin.email.notify_materials_header','Notificaciones de materiales'),
        ('de','admin.email.notify_materials_header','Materialbenachrichtigungen'),
        ('it','admin.email.notify_mat_created','Notifica creazione materiale'),
        ('en','admin.email.notify_mat_created','Notify on material creation'),
        ('es','admin.email.notify_mat_created','Notificar al crear material'),
        ('de','admin.email.notify_mat_created','Benachrichtigung bei Materialerstellung'),
        ('it','admin.email.notify_mat_created_desc','Invia un''email ai ruoli selezionati quando un materiale viene creato.'),
        ('en','admin.email.notify_mat_created_desc','Send an email to selected roles when a material is created.'),
        ('es','admin.email.notify_mat_created_desc','Envía un correo a los roles seleccionados cuando se crea un material.'),
        ('de','admin.email.notify_mat_created_desc','Sendet eine E-Mail an ausgewählte Rollen, wenn ein Material erstellt wird.'),
        ('it','admin.email.notify_mat_updated','Notifica modifica materiale'),
        ('en','admin.email.notify_mat_updated','Notify on material update'),
        ('es','admin.email.notify_mat_updated','Notificar al modificar material'),
        ('de','admin.email.notify_mat_updated','Benachrichtigung bei Materialänderung'),
        ('it','admin.email.notify_mat_updated_desc','Invia un''email ai ruoli selezionati quando un materiale viene modificato.'),
        ('en','admin.email.notify_mat_updated_desc','Send an email to selected roles when a material is updated.'),
        ('es','admin.email.notify_mat_updated_desc','Envía un correo a los roles seleccionados cuando se modifica un material.'),
        ('de','admin.email.notify_mat_updated_desc','Sendet eine E-Mail an ausgewählte Rollen, wenn ein Material geändert wird.'),
        ('it','admin.email.notify_mat_deleted','Notifica eliminazione materiale'),
        ('en','admin.email.notify_mat_deleted','Notify on material deletion'),
        ('es','admin.email.notify_mat_deleted','Notificar al eliminar material'),
        ('de','admin.email.notify_mat_deleted','Benachrichtigung bei Materiallöschung'),
        ('it','admin.email.notify_mat_deleted_desc','Invia un''email ai ruoli selezionati quando un materiale viene eliminato.'),
        ('en','admin.email.notify_mat_deleted_desc','Send an email to selected roles when a material is deleted.'),
        ('es','admin.email.notify_mat_deleted_desc','Envía un correo a los roles seleccionados cuando se elimina un material.'),
        ('de','admin.email.notify_mat_deleted_desc','Sendet eine E-Mail an ausgewählte Rollen, wenn ein Material gelöscht wird.'),
        ('it','admin.email.notify_courses','Notifiche corsi'),
        ('en','admin.email.notify_courses','Course notifications'),
        ('es','admin.email.notify_courses','Notificaciones de cursos'),
        ('de','admin.email.notify_courses','Kursbenachrichtigungen'),
        ('it','admin.email.notify_courses_desc','Abilita le notifiche email relative ai corsi (iscrizioni, quiz).'),
        ('en','admin.email.notify_courses_desc','Enable email notifications related to courses (enrollments, quizzes).'),
        ('es','admin.email.notify_courses_desc','Activa las notificaciones de correo relacionadas con los cursos (inscripciones, quizzes).'),
        ('de','admin.email.notify_courses_desc','Aktiviert E-Mail-Benachrichtigungen zu Kursen (Einschreibungen, Quiz).'),
        ('it','admin.email.notify_student_enroll','Notifica allo studente all''iscrizione al corso'),
        ('en','admin.email.notify_student_enroll','Notify student on course enrollment'),
        ('es','admin.email.notify_student_enroll','Notificar al estudiante al inscribirse en el curso'),
        ('de','admin.email.notify_student_enroll','Student bei Kurseinschreibung benachrichtigen'),
        ('it','admin.email.notify_student_quiz','Notifica allo studente al completamento del quiz'),
        ('en','admin.email.notify_student_quiz','Notify student on quiz completion'),
        ('es','admin.email.notify_student_quiz','Notificar al estudiante al completar el quiz'),
        ('de','admin.email.notify_student_quiz','Student bei Quiz-Abschluss benachrichtigen'),
        ('it','admin.email.notify_teacher_quiz','Notifica al docente al completamento del quiz'),
        ('en','admin.email.notify_teacher_quiz','Notify teacher on quiz completion'),
        ('es','admin.email.notify_teacher_quiz','Notificar al docente al completarse el quiz'),
        ('de','admin.email.notify_teacher_quiz','Lehrer bei Quiz-Abschluss benachrichtigen'),
        ('it','admin.email.notify_teacher_enroll','Notifica al docente quando uno studente si iscrive'),
        ('en','admin.email.notify_teacher_enroll','Notify teacher when a student enrolls'),
        ('es','admin.email.notify_teacher_enroll','Notificar al docente cuando un estudiante se inscribe'),
        ('de','admin.email.notify_teacher_enroll','Lehrer benachrichtigen, wenn ein Student sich einschreibt')
        ON DUPLICATE KEY UPDATE label_value = VALUES(label_value);", conn);
    await ins.ExecuteNonQueryAsync();
}
catch { }

// ── Seed admin.timezone.* + dashboard.local_time keys ────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT INTO translations (language_code, label_key, label_value) VALUES
        ('it','admin.timezone.title','Fuso orario piattaforma'),
        ('en','admin.timezone.title','Platform timezone'),
        ('es','admin.timezone.title','Zona horaria de la plataforma'),
        ('de','admin.timezone.title','Plattform-Zeitzone'),
        ('it','admin.timezone.desc','Il fuso orario usato per l''orologio nella dashboard di tutti gli utenti. Se il browser è in un fuso diverso, viene mostrata anche l''ora locale.'),
        ('en','admin.timezone.desc','The timezone used for the clock on all users'' dashboards. If the browser is in a different timezone, the local time is also shown.'),
        ('es','admin.timezone.desc','La zona horaria usada para el reloj en el panel de todos los usuarios. Si el navegador tiene una zona diferente, también se muestra la hora local.'),
        ('de','admin.timezone.desc','Die Zeitzone für die Uhr auf dem Dashboard aller Benutzer. Wenn der Browser eine andere Zeitzone hat, wird auch die lokale Zeit angezeigt.'),
        ('it','admin.timezone.save','Salva fuso orario'),
        ('en','admin.timezone.save','Save timezone'),
        ('es','admin.timezone.save','Guardar zona horaria'),
        ('de','admin.timezone.save','Zeitzone speichern'),
        ('it','admin.timezone.current','Attuale:'),
        ('en','admin.timezone.current','Current:'),
        ('es','admin.timezone.current','Actual:'),
        ('de','admin.timezone.current','Aktuell:'),
        ('it','dashboard.local_time','Ora locale del browser'),
        ('en','dashboard.local_time','Browser local time'),
        ('es','dashboard.local_time','Hora local del navegador'),
        ('de','dashboard.local_time','Lokale Browserzeit')
        ON DUPLICATE KEY UPDATE label_value = VALUES(label_value);", conn);
    await ins.ExecuteNonQueryAsync();
}
catch { }

// ── Seed dashboard.* translation keys ────────────────────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT INTO translations (language_code, label_key, label_value) VALUES
        ('it','dashboard.title','Dashboard'),
        ('en','dashboard.title','Dashboard'),
        ('es','dashboard.title','Panel de control'),
        ('de','dashboard.title','Dashboard'),
        ('it','dashboard.admin.title','Dashboard Admin'),
        ('en','dashboard.admin.title','Admin Dashboard'),
        ('es','dashboard.admin.title','Panel de administración'),
        ('de','dashboard.admin.title','Admin-Dashboard'),
        ('it','dashboard.admin.stats','Panoramica piattaforma'),
        ('en','dashboard.admin.stats','Platform overview'),
        ('es','dashboard.admin.stats','Resumen de la plataforma'),
        ('de','dashboard.admin.stats','Plattformübersicht'),
        ('it','dashboard.admin.courses','Corsi totali'),
        ('en','dashboard.admin.courses','Total courses'),
        ('es','dashboard.admin.courses','Cursos totales'),
        ('de','dashboard.admin.courses','Kurse gesamt'),
        ('it','dashboard.admin.students','Studenti attivi'),
        ('en','dashboard.admin.students','Active students'),
        ('es','dashboard.admin.students','Estudiantes activos'),
        ('de','dashboard.admin.students','Aktive Studenten'),
        ('it','dashboard.admin.teachers','Docenti attivi'),
        ('en','dashboard.admin.teachers','Active teachers'),
        ('es','dashboard.admin.teachers','Docentes activos'),
        ('de','dashboard.admin.teachers','Aktive Lehrkräfte'),
        ('it','dashboard.admin.enrollments','Iscrizioni totali'),
        ('en','dashboard.admin.enrollments','Total enrollments'),
        ('es','dashboard.admin.enrollments','Inscripciones totales'),
        ('de','dashboard.admin.enrollments','Einschreibungen gesamt'),
        ('it','dashboard.admin.materials','Materiali totali'),
        ('en','dashboard.admin.materials','Total materials'),
        ('es','dashboard.admin.materials','Materiales totales'),
        ('de','dashboard.admin.materials','Materialien gesamt'),
        ('it','dashboard.admin.users','Utenti totali'),
        ('en','dashboard.admin.users','Total users'),
        ('es','dashboard.admin.users','Usuarios totales'),
        ('de','dashboard.admin.users','Benutzer gesamt'),
        ('it','dashboard.materials.total','Materiali totali'),
        ('en','dashboard.materials.total','Total materials'),
        ('es','dashboard.materials.total','Materiales totales'),
        ('de','dashboard.materials.total','Materialien gesamt'),
        ('it','dashboard.materials.recent','Aggiunti negli ultimi 30 giorni'),
        ('en','dashboard.materials.recent','Added in the last 30 days'),
        ('es','dashboard.materials.recent','Añadidos en los últimos 30 días'),
        ('de','dashboard.materials.recent','In den letzten 30 Tagen hinzugefügt'),
        ('it','dashboard.teacher.courses','I miei corsi'),
        ('en','dashboard.teacher.courses','My courses'),
        ('es','dashboard.teacher.courses','Mis cursos'),
        ('de','dashboard.teacher.courses','Meine Kurse'),
        ('it','dashboard.teacher.students','Studenti iscritti'),
        ('en','dashboard.teacher.students','Enrolled students'),
        ('es','dashboard.teacher.students','Estudiantes inscritos'),
        ('de','dashboard.teacher.students','Eingeschriebene Studenten'),
        ('it','dashboard.student.enrolled','Corsi iscritti'),
        ('en','dashboard.student.enrolled','Enrolled courses'),
        ('es','dashboard.student.enrolled','Cursos inscritos'),
        ('de','dashboard.student.enrolled','Eingeschriebene Kurse'),
        ('it','dashboard.student.completed','Lezioni completate'),
        ('en','dashboard.student.completed','Completed lessons'),
        ('es','dashboard.student.completed','Lecciones completadas'),
        ('de','dashboard.student.completed','Abgeschlossene Lektionen'),
        ('it','dashboard.pending.title','Account in attesa'),
        ('en','dashboard.pending.title','Account pending'),
        ('es','dashboard.pending.title','Cuenta pendiente'),
        ('de','dashboard.pending.title','Konto ausstehend'),
        ('it','dashboard.pending.desc','Il tuo account è stato registrato. Un amministratore ti assegnerà presto un ruolo per accedere alle funzionalità della piattaforma.'),
        ('en','dashboard.pending.desc','Your account has been registered. An administrator will soon assign you a role to access the platform features.'),
        ('es','dashboard.pending.desc','Tu cuenta ha sido registrada. Un administrador te asignará pronto un rol para acceder a las funcionalidades de la plataforma.'),
        ('de','dashboard.pending.desc','Dein Konto wurde registriert. Ein Administrator wird dir bald eine Rolle zuweisen, um auf die Plattformfunktionen zugreifen zu können.'),
        ('it','dashboard.no_modules','Nessun modulo attivo. Contatta l''amministratore.'),
        ('en','dashboard.no_modules','No active modules. Please contact your administrator.'),
        ('es','dashboard.no_modules','No hay módulos activos. Por favor, contacta con el administrador.'),
        ('de','dashboard.no_modules','Keine aktiven Module. Bitte wende dich an den Administrator.')
        ON DUPLICATE KEY UPDATE label_value = VALUES(label_value);", conn);
    await ins.ExecuteNonQueryAsync();
}
catch { }

// ── Migrate: add can_teach / can_attend to roles; seed Teacher & Student ───
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();

    using var addCols = new MySqlConnector.MySqlCommand(@"
        ALTER TABLE roles
            ADD COLUMN IF NOT EXISTS can_teach  TINYINT(1) NOT NULL DEFAULT 0,
            ADD COLUMN IF NOT EXISTS can_attend TINYINT(1) NOT NULL DEFAULT 0;", conn);
    await addCols.ExecuteNonQueryAsync();

    using var seedRoles = new MySqlConnector.MySqlCommand(@"
        INSERT INTO roles (name, normalized_name, can_teach, can_attend)
        VALUES ('Teacher', 'TEACHER', 1, 0),
               ('Student', 'STUDENT', 0, 1)
        ON DUPLICATE KEY UPDATE
            can_teach  = VALUES(can_teach),
            can_attend = VALUES(can_attend);", conn);
    await seedRoles.ExecuteNonQueryAsync();
}
catch { }

// ── Migrate: fix users.role DEFAULT (remove 'Student') ────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var fixDefault = new MySqlConnector.MySqlCommand(
        "ALTER TABLE users MODIFY COLUMN role VARCHAR(50) NOT NULL DEFAULT '';", conn);
    await fixDefault.ExecuteNonQueryAsync();
}
catch { }

// ── Seed translations: ruolo can_teach / can_attend ───────────────────────
try
{
    var dbHelper = app.Services.GetRequiredService<DbHelper>();
    using var conn = dbHelper.GetConnection();
    await conn.OpenAsync();
    using var ins = new MySqlConnector.MySqlCommand(@"
        INSERT INTO translations (language_code, label_key, label_value) VALUES
        ('en','admin.role_course_participation','Course participation'),
        ('it','admin.role_course_participation','Partecipazione ai corsi'),
        ('es','admin.role_course_participation','Participación en cursos'),
        ('de','admin.role_course_participation','Kurs-Teilnahme'),
        ('en','admin.role_course_participation_hint','Defines how users with this role interact with courses.'),
        ('it','admin.role_course_participation_hint','Definisce come gli utenti con questo ruolo interagiscono con i corsi.'),
        ('es','admin.role_course_participation_hint','Define cómo los usuarios con este rol interactúan con los cursos.'),
        ('de','admin.role_course_participation_hint','Legt fest, wie Benutzer mit dieser Rolle mit Kursen interagieren.'),
        ('en','admin.role_can_teach','Teach a course'),
        ('it','admin.role_can_teach','Sostieni corso'),
        ('es','admin.role_can_teach','Impartir curso'),
        ('de','admin.role_can_teach','Kurs leiten'),
        ('en','admin.role_can_teach_hint','Can create and manage courses as a teacher.'),
        ('it','admin.role_can_teach_hint','Può creare e gestire corsi come docente.'),
        ('es','admin.role_can_teach_hint','Puede crear y gestionar cursos como docente.'),
        ('de','admin.role_can_teach_hint','Kann Kurse als Lehrender erstellen und verwalten.'),
        ('en','admin.role_can_attend','Attend a course'),
        ('it','admin.role_can_attend','Partecipa al corso'),
        ('es','admin.role_can_attend','Participar en el curso'),
        ('de','admin.role_can_attend','Kurs besuchen'),
        ('en','admin.role_can_attend_hint','Can enroll in and attend courses as a student.'),
        ('it','admin.role_can_attend_hint','Può iscriversi e frequentare i corsi come studente.'),
        ('es','admin.role_can_attend_hint','Puede inscribirse y asistir a cursos como estudiante.'),
        ('de','admin.role_can_attend_hint','Kann sich als Lernender einschreiben und Kurse besuchen.')
        ON DUPLICATE KEY UPDATE label_value = VALUES(label_value);", conn);
    await ins.ExecuteNonQueryAsync();
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
