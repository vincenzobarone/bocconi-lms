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
        ('it','admin.disable_courses_confirm','Disabilitare il modulo Corsi? Studenti e docenti accederanno direttamente alla libreria Materiali.');", conn);
    await ins.ExecuteNonQueryAsync();
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
        ('en','admin.edit_role','Edit role name'),
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
        ('it','admin.edit_role','Modifica nome ruolo'),
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
