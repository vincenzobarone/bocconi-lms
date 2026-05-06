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

builder.Services.AddControllersWithViews();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ── Seed traduzioni nuove chiavi (idempotente, background) ─────────────────
_ = Task.Run(async () =>
{
    await Task.Delay(2000); // attendi che il DI container sia pronto
    using var scope = app.Services.CreateScope();
    var tr = scope.ServiceProvider.GetRequiredService<TranslationRepository>();
    var seeds = new (string lang, string key, string val)[]
    {
        ("en", "dashboard.teacher.my_courses",   "My Courses"),
        ("it", "dashboard.teacher.my_courses",   "I miei corsi"),
        ("en", "dashboard.teacher.manage_btn",   "Manage"),
        ("it", "dashboard.teacher.manage_btn",   "Gestisci"),
        ("en", "dashboard.teacher.no_courses",   "You haven\u2019t created any courses yet. "),
        ("it", "dashboard.teacher.no_courses",   "Non hai ancora creato nessun corso. "),
        ("en", "dashboard.teacher.create_first", "Create your first course"),
        ("it", "dashboard.teacher.create_first", "Crea il tuo primo corso"),
        ("en", "course.no_description",          "No description provided."),
        ("it", "course.no_description",          "Nessuna descrizione disponibile."),
        ("en", "course.create",                  "New Course"),
        ("it", "course.create",                  "Nuovo corso"),
        ("en", "course.draft",                   "Draft"),
        ("it", "course.draft",                   "Bozza"),
        ("en", "course.published",               "Published"),
        ("it", "course.published",               "Pubblicato"),
        // Stats page
        ("en", "course.stats.title",             "Course Analytics"),
        ("it", "course.stats.title",             "Analytics corso"),
        ("en", "course.stats.enrolled",          "Enrolled"),
        ("it", "course.stats.enrolled",          "Iscritti"),
        ("en", "course.stats.lessons",           "Lessons"),
        ("it", "course.stats.lessons",           "Lezioni"),
        ("en", "course.stats.quizzes",           "Quizzes"),
        ("it", "course.stats.quizzes",           "Quiz"),
        ("en", "course.stats.total_attempts",    "Total Attempts"),
        ("it", "course.stats.total_attempts",    "Tentativi totali"),
        ("en", "course.stats.lessons_chart",     "Lesson Completion"),
        ("it", "course.stats.lessons_chart",     "Completamento lezioni"),
        ("en", "course.stats.enrolled_of",       "out of"),
        ("it", "course.stats.enrolled_of",       "su"),
        ("en", "course.stats.students",          "enrolled students"),
        ("it", "course.stats.students",          "studenti iscritti"),
        ("en", "course.stats.students_completed","Students who completed"),
        ("it", "course.stats.students_completed","Studenti che hanno completato"),
        ("en", "course.stats.no_lessons",        "No published lessons yet."),
        ("it", "course.stats.no_lessons",        "Nessuna lezione pubblicata."),
        ("en", "course.stats.quiz_chart",        "Quiz Results"),
        ("it", "course.stats.quiz_chart",        "Risultati quiz"),
        ("en", "course.stats.no_quizzes",        "No quizzes available."),
        ("it", "course.stats.no_quizzes",        "Nessun quiz disponibile."),
        ("en", "course.stats.avg_score",         "Avg. Score"),
        ("it", "course.stats.avg_score",         "Punteggio medio"),
        ("en", "course.stats.pass_rate",         "Pass rate (% enrolled)"),
        ("it", "course.stats.pass_rate",         "% superato (su iscritti)"),
        ("en", "course.stats.passing_score",     "Passing threshold"),
        ("it", "course.stats.passing_score",     "Soglia di superamento"),
        ("en", "course.stats.col_quiz",          "Quiz"),
        ("it", "course.stats.col_quiz",          "Quiz"),
        ("en", "course.stats.col_lesson",        "Lesson"),
        ("it", "course.stats.col_lesson",        "Lezione"),
        ("en", "course.stats.col_attempts",      "Attempts"),
        ("it", "course.stats.col_attempts",      "Tentativi"),
        ("en", "course.stats.col_students",      "Students"),
        ("it", "course.stats.col_students",      "Studenti"),
        ("en", "course.stats.col_avg",           "Avg"),
        ("it", "course.stats.col_avg",           "Media"),
        ("en", "course.stats.col_max",           "Max"),
        ("it", "course.stats.col_max",           "Max"),
        ("en", "course.stats.col_passed",        "Passed"),
        ("it", "course.stats.col_passed",        "Superati"),
        ("en", "course.stats.col_threshold",     "Threshold"),
        ("it", "course.stats.col_threshold",     "Soglia"),

        // ── Alert messages (§key convention) ─────────────────────────────

        // account
        ("en", "account.msg_password_changed",       "Password changed successfully."),
        ("it", "account.msg_password_changed",       "Password modificata con successo."),

        // lesson
        ("en", "lesson.msg_created",                 "Lesson created."),
        ("it", "lesson.msg_created",                 "Lezione creata."),
        ("en", "lesson.msg_updated",                 "Lesson updated."),
        ("it", "lesson.msg_updated",                 "Lezione aggiornata."),
        ("en", "lesson.msg_deleted",                 "Lesson deleted."),
        ("it", "lesson.msg_deleted",                 "Lezione eliminata."),

        // quiz
        ("en", "quiz.msg_created",                   "Quiz created."),
        ("it", "quiz.msg_created",                   "Quiz creato."),
        ("en", "quiz.msg_deleted",                   "Quiz deleted."),
        ("it", "quiz.msg_deleted",                   "Quiz eliminato."),

        // course
        ("en", "course.msg_enrolled",                "Enrolled successfully."),
        ("it", "course.msg_enrolled",                "Iscrizione completata."),
        ("en", "course.msg_unenrolled",              "Unenrolled successfully."),
        ("it", "course.msg_unenrolled",              "Disiscrizione completata."),
        ("en", "course.msg_created",                 "Course created."),
        ("it", "course.msg_created",                 "Corso creato."),
        ("en", "course.msg_updated",                 "Course updated."),
        ("it", "course.msg_updated",                 "Corso aggiornato."),
        ("en", "course.msg_deleted",                 "Course deleted."),
        ("it", "course.msg_deleted",                 "Corso eliminato."),

        // materials
        ("en", "mat.msg_created",                    "Material \u00ab{0}\u00bb created."),
        ("it", "mat.msg_created",                    "Materiale \u00ab{0}\u00bb creato."),
        ("en", "mat.msg_created_no_file",            "Material saved, but the physical file could not be written: {0}"),
        ("it", "mat.msg_created_no_file",            "Materiale salvato, ma il file fisico non \u00e8 stato scritto: {0}"),
        ("en", "mat.msg_updated",                    "Material updated."),
        ("it", "mat.msg_updated",                    "Materiale aggiornato."),
        ("en", "mat.msg_updated_no_file",            "Material updated, but the physical file could not be written: {0}"),
        ("it", "mat.msg_updated_no_file",            "Materiale aggiornato, ma il file fisico non \u00e8 stato scritto: {0}"),
        ("en", "mat.msg_select_file",                "No file selected."),
        ("it", "mat.msg_select_file",                "Nessun file selezionato."),
        ("en", "mat.msg_file_save_error",            "Error saving the file: {0}"),
        ("it", "mat.msg_file_save_error",            "Errore nel salvataggio del file: {0}"),
        ("en", "mat.msg_version_uploaded",           "New version uploaded."),
        ("it", "mat.msg_version_uploaded",           "Nuova versione caricata."),
        ("en", "mat.msg_version_restored",           "Version restored."),
        ("it", "mat.msg_version_restored",           "Versione ripristinata."),
        ("en", "mat.msg_version_last",               "Cannot delete the last version of a material."),
        ("it", "mat.msg_version_last",               "Non puoi eliminare l'ultima versione di un materiale."),
        ("en", "mat.msg_version_deleted",            "Version {0} deleted."),
        ("it", "mat.msg_version_deleted",            "Versione {0} eliminata."),
        ("en", "mat.msg_select_at_least_one",        "Select at least one file."),
        ("it", "mat.msg_select_at_least_one",        "Seleziona almeno un file."),
        ("en", "mat.msg_no_files_available",         "No files available for download."),
        ("it", "mat.msg_no_files_available",         "Nessun file disponibile per il download."),
        ("en", "mat.msg_file_not_found_admin",       "File \u00ab{0}\u00bb is not available on the server. Contact the administrator or re-upload the document."),
        ("it", "mat.msg_file_not_found_admin",       "Il file \u00ab{0}\u00bb non \u00e8 disponibile sul server. Contatta l'amministratore o ricarica il documento."),
        ("en", "mat.msg_file_not_found",             "File \u00ab{0}\u00bb is not available on the server."),
        ("it", "mat.msg_file_not_found",             "Il file \u00ab{0}\u00bb non \u00e8 disponibile sul server."),
        ("en", "mat.msg_deleted",                    "Material \u00ab{0}\u00bb deleted."),
        ("it", "mat.msg_deleted",                    "Materiale \u00ab{0}\u00bb eliminato."),
        ("en", "mat.msg_linked_lesson",              "Material linked to the lesson."),
        ("it", "mat.msg_linked_lesson",              "Materiale collegato alla lezione."),
        ("en", "mat.msg_unlinked_lesson",            "Material unlinked from the lesson."),
        ("it", "mat.msg_unlinked_lesson",            "Materiale rimosso dalla lezione."),

        // mat – UI labels (views)
        ("en", "mat.nav",                            "Materials"),
        ("it", "mat.nav",                            "Materiali"),
        ("en", "mat.page_title",                     "Materials"),
        ("it", "mat.page_title",                     "Materiali"),
        ("en", "mat.new_btn",                        "New material"),
        ("it", "mat.new_btn",                        "Nuovo materiale"),
        ("en", "mat.create_btn",                     "Create material"),
        ("it", "mat.create_btn",                     "Crea materiale"),
        ("en", "mat.create_title",                   "New material"),
        ("it", "mat.create_title",                   "Nuovo materiale"),
        ("en", "mat.edit_title",                     "Edit material"),
        ("it", "mat.edit_title",                     "Modifica materiale"),
        ("en", "mat.save_btn",                       "Save"),
        ("it", "mat.save_btn",                       "Salva"),
        ("en", "mat.cancel",                         "Cancel"),
        ("it", "mat.cancel",                         "Annulla"),
        ("en", "mat.back",                           "Back"),
        ("it", "mat.back",                           "Indietro"),
        ("en", "mat.label_title",                    "Title"),
        ("it", "mat.label_title",                    "Titolo"),
        ("en", "mat.title_placeholder",              "Document title"),
        ("it", "mat.title_placeholder",              "Titolo del documento"),
        ("en", "mat.title_required",                 "Title is required."),
        ("it", "mat.title_required",                 "Il titolo è obbligatorio."),
        ("en", "mat.label_author",                   "Author"),
        ("it", "mat.label_author",                   "Autore"),
        ("en", "mat.author_placeholder",             "Author name"),
        ("it", "mat.author_placeholder",             "Nome autore"),
        ("en", "mat.label_doctype",                  "Document type"),
        ("it", "mat.label_doctype",                  "Tipo documento"),
        ("en", "mat.doctype_required",               "Document type is required."),
        ("it", "mat.doctype_required",               "Il tipo documento è obbligatorio."),
        ("en", "mat.select_type",                    "— Select type —"),
        ("it", "mat.select_type",                    "— Seleziona tipo —"),
        ("en", "mat.label_language",                 "Language"),
        ("it", "mat.label_language",                 "Lingua"),
        ("en", "mat.label_status",                   "Status"),
        ("it", "mat.label_status",                   "Stato"),
        ("en", "mat.status_bozza",                   "Draft"),
        ("it", "mat.status_bozza",                   "Bozza"),
        ("en", "mat.status_in_revisione",            "Under review"),
        ("it", "mat.status_in_revisione",            "In revisione"),
        ("en", "mat.status_verificato",              "Verified"),
        ("it", "mat.status_verificato",              "Verificato"),
        ("en", "mat.status_locked_edit",             "You cannot change the status."),
        ("it", "mat.status_locked_edit",             "Non puoi modificare lo stato."),
        ("en", "mat.label_owner",                    "Owner"),
        ("it", "mat.label_owner",                    "Proprietario"),
        ("en", "mat.label_folder",                   "Folder"),
        ("it", "mat.label_folder",                   "Cartella"),
        ("en", "mat.label_area",                     "Area"),
        ("it", "mat.label_area",                     "Area"),
        ("en", "mat.select_area",                    "— Select area —"),
        ("it", "mat.select_area",                    "— Seleziona area —"),
        ("en", "mat.label_cat_date",                 "Catalogation date"),
        ("it", "mat.label_cat_date",                 "Data catalogazione"),
        ("en", "mat.label_notes",                    "Notes"),
        ("it", "mat.label_notes",                    "Note"),
        ("en", "mat.label_platform",                 "Platform"),
        ("it", "mat.label_platform",                 "Piattaforma"),
        ("en", "mat.select_platform",                "— Select platform —"),
        ("it", "mat.select_platform",                "— Seleziona piattaforma —"),
        ("en", "mat.label_publishable",              "Publishable"),
        ("it", "mat.label_publishable",              "Pubblicabile"),
        ("en", "mat.label_published",                "Published"),
        ("it", "mat.label_published",                "Pubblicato"),
        ("en", "mat.label_ext_protocol",             "External protocol code"),
        ("it", "mat.label_ext_protocol",             "Codice protocollo esterno"),
        ("en", "mat.label_ext_link",                 "External link"),
        ("it", "mat.label_ext_link",                 "Link esterno"),
        ("en", "mat.badge_publishable",              "Publishable"),
        ("it", "mat.badge_publishable",              "Pubblicabile"),
        ("en", "mat.badge_published",                "Published"),
        ("it", "mat.badge_published",                "Pubblicato"),
        ("en", "mat.publish_section",                "Publication"),
        ("it", "mat.publish_section",                "Pubblicazione"),
        ("en", "mat.protocol_number",                "Protocol number"),
        ("it", "mat.protocol_number",                "Numero protocollo"),
        ("en", "mat.protocol_auto",                  "Assigned automatically on verification"),
        ("it", "mat.protocol_auto",                  "Assegnato automaticamente alla verifica"),
        ("en", "mat.verified_fields",                "Verified fields"),
        ("it", "mat.verified_fields",                "Campi verificati"),
        ("en", "mat.verified_modal_title",           "Verify material"),
        ("it", "mat.verified_modal_title",           "Verifica materiale"),
        ("en", "mat.verified_modal_hint",            "Enter the folder to assign the protocol number."),
        ("it", "mat.verified_modal_hint",            "Inserisci la cartella per assegnare il numero protocollo."),
        ("en", "mat.modal_confirm",                  "Confirm"),
        ("it", "mat.modal_confirm",                  "Conferma"),
        ("en", "mat.folder_filter",                  "Search folder…"),
        ("it", "mat.folder_filter",                  "Cerca cartella…"),
        ("en", "mat.folder_new",                     "＋ New folder"),
        ("it", "mat.folder_new",                     "＋ Nuova cartella"),
        ("en", "mat.folder_new_placeholder",         "Folder name"),
        ("it", "mat.folder_new_placeholder",         "Nome cartella"),
        ("en", "mat.folder_required",                "Select or create a folder."),
        ("it", "mat.folder_required",                "Seleziona o crea una cartella."),
        ("en", "mat.page_count",                     "Pages"),
        ("it", "mat.page_count",                     "Pagine"),
        ("en", "mat.pages",                          "pages"),
        ("it", "mat.pages",                          "pagine"),
        ("en", "mat.slides",                         "slides"),
        ("it", "mat.slides",                         "slide"),
        ("en", "mat.upload_document",                "Upload document"),
        ("it", "mat.upload_document",                "Carica Documento"),
        ("en", "mat.upload_hint",                    "Click to select a file from your computer"),
        ("it", "mat.upload_hint",                    "Clicca per selezionare un file dal tuo computer"),
        ("en", "mat.upload_version",                 "Upload new version"),
        ("it", "mat.upload_version",                 "Carica nuova versione"),
        ("en", "mat.upload_btn",                     "Upload"),
        ("it", "mat.upload_btn",                     "Carica"),
        ("en", "mat.remove_file",                    "Remove file"),
        ("it", "mat.remove_file",                    "Rimuovi file"),
        ("en", "mat.convert_to_pdf",                 "Convert to PDF before saving"),
        ("it", "mat.convert_to_pdf",                 "Converti in PDF prima del salvataggio"),
        ("en", "mat.file_lost_warn",                 "The file was lost after the form was submitted. Please re-upload it."),
        ("it", "mat.file_lost_warn",                 "Il file è andato perso dopo l'invio del modulo. Ricaricalo."),
        ("en", "mat.file_required_warn",             "Please upload a file."),
        ("it", "mat.file_required_warn",             "Carica un file."),
        ("en", "mat.label_file",                     "File"),
        ("it", "mat.label_file",                     "File"),
        ("en", "mat.choose_file",                    "Choose file"),
        ("it", "mat.choose_file",                    "Scegli file"),
        ("en", "mat.no_file_chosen",                 "No file chosen"),
        ("it", "mat.no_file_chosen",                 "Nessun file scelto"),
        ("en", "mat.notes",                          "Notes"),
        ("it", "mat.notes",                          "Note"),
        ("en", "mat.notes_placeholder",              "What changed in this version?"),
        ("it", "mat.notes_placeholder",              "Cosa è cambiato in questa versione?"),
        ("en", "mat.notes_file_placeholder",         "Optional notes on the file"),
        ("it", "mat.notes_file_placeholder",         "Note facoltative sul file"),
        ("en", "mat.new_version_notes",              "New version notes"),
        ("it", "mat.new_version_notes",              "Note sulla nuova versione"),
        ("en", "mat.version_notes_placeholder",      "What changes in this version?"),
        ("it", "mat.version_notes_placeholder",      "Quali modifiche in questa versione?"),
        ("en", "mat.active_version_label",           "Active version:"),
        ("it", "mat.active_version_label",           "Versione attiva:"),
        ("en", "mat.new_file",                       "New file"),
        ("it", "mat.new_file",                       "Nuovo file"),
        ("en", "mat.file_hint",                      "Leave empty to not update the file."),
        ("it", "mat.file_hint",                      "Lascia vuoto per non aggiornare il file."),
        ("en", "mat.versions",                       "Versions"),
        ("it", "mat.versions",                       "Versioni"),
        ("en", "mat.version_active",                 "Active"),
        ("it", "mat.version_active",                 "Attiva"),
        ("en", "mat.no_files",                       "No files uploaded yet."),
        ("it", "mat.no_files",                       "Nessun file caricato."),
        ("en", "mat.download_btn",                   "Download"),
        ("it", "mat.download_btn",                   "Scarica"),
        ("en", "mat.restore_btn",                    "Restore"),
        ("it", "mat.restore_btn",                    "Ripristina"),
        ("en", "mat.restore_confirm",                "Restore version"),
        ("it", "mat.restore_confirm",                "Ripristina versione"),
        ("en", "mat.delete_version_btn",             "Delete version"),
        ("it", "mat.delete_version_btn",             "Elimina versione"),
        ("en", "mat.delete_version_confirm",         "Delete version"),
        ("it", "mat.delete_version_confirm",         "Elimina versione"),
        ("en", "mat.info_panel",                     "Information"),
        ("it", "mat.info_panel",                     "Informazioni"),
        ("en", "mat.col_type",                       "Type"),
        ("it", "mat.col_type",                       "Tipo"),
        ("en", "mat.col_lang",                       "Language"),
        ("it", "mat.col_lang",                       "Lingua"),
        ("en", "mat.col_author",                     "Author"),
        ("it", "mat.col_author",                     "Autore"),
        ("en", "mat.col_status",                     "Status"),
        ("it", "mat.col_status",                     "Stato"),
        ("en", "mat.col_version",                    "Version"),
        ("it", "mat.col_version",                    "Versione"),
        ("en", "mat.col_created",                    "Created"),
        ("it", "mat.col_created",                    "Creato"),
        ("en", "mat.col_protocol",                   "Protocol"),
        ("it", "mat.col_protocol",                   "Protocollo"),
        ("en", "mat.search_placeholder",             "Search by title, author, type…"),
        ("it", "mat.search_placeholder",             "Cerca per titolo, autore, tipo…"),
        ("en", "mat.filter_title",                   "Title"),
        ("it", "mat.filter_title",                   "Titolo"),
        ("en", "mat.filter_type",                    "Type"),
        ("it", "mat.filter_type",                    "Tipo"),
        ("en", "mat.filter_lang",                    "Language"),
        ("it", "mat.filter_lang",                    "Lingua"),
        ("en", "mat.filter_folder_id",               "Folder"),
        ("it", "mat.filter_folder_id",               "Cartella"),
        ("en", "mat.filter_folder_name",             "Folder name"),
        ("it", "mat.filter_folder_name",             "Nome cartella"),
        ("en", "mat.filter_folder_name_ph",          "Filter by folder…"),
        ("it", "mat.filter_folder_name_ph",          "Filtra per cartella…"),
        ("en", "mat.filter_cat_year",                "Cat. year"),
        ("it", "mat.filter_cat_year",                "Anno cat."),
        ("en", "mat.filter_mod_year",                "Mod. year"),
        ("it", "mat.filter_mod_year",                "Anno mod."),
        ("en", "mat.all_langs",                      "All languages"),
        ("it", "mat.all_langs",                      "Tutte le lingue"),
        ("en", "mat.all_types",                      "All types"),
        ("it", "mat.all_types",                      "Tutti i tipi"),
        ("en", "mat.results_count",                  "{0} results"),
        ("it", "mat.results_count",                  "{0} risultati"),
        ("en", "mat.no_results",                     "No materials found."),
        ("it", "mat.no_results",                     "Nessun materiale trovato."),
        ("en", "mat.no_results_student",             "No materials available yet."),
        ("it", "mat.no_results_student",             "Nessun materiale disponibile."),
        ("en", "mat.create_first",                   "Create the first material"),
        ("it", "mat.create_first",                   "Crea il primo materiale"),
        ("en", "mat.bulk_download",                  "Download selected"),
        ("it", "mat.bulk_download",                  "Scarica selezionati"),
        ("en", "mat.export_excel",                   "Export Excel"),
        ("it", "mat.export_excel",                   "Esporta Excel"),
        ("en", "mat.export_excel_hint",              "Download the current list as .xlsx"),
        ("it", "mat.export_excel_hint",              "Scarica la lista corrente come .xlsx"),
        ("en", "mat.export_pdf",                     "Export PDF"),
        ("it", "mat.export_pdf",                     "Esporta PDF"),
        ("en", "mat.export_pdf_hint",                "Download the current list as .pdf"),
        ("it", "mat.export_pdf_hint",                "Scarica la lista corrente come .pdf"),
        ("en", "mat.select_all",                     "Select all"),
        ("it", "mat.select_all",                     "Seleziona tutti"),
        ("en", "mat.deselect_all",                   "Deselect all"),
        ("it", "mat.deselect_all",                   "Deseleziona tutti"),
        ("en", "mat.selected",                       "{0} selected"),
        ("it", "mat.selected",                       "{0} selezionati"),
        ("en", "mat.details_btn",                    "Details"),
        ("it", "mat.details_btn",                    "Dettagli"),
        ("en", "mat.details_aria",                   "View details"),
        ("it", "mat.details_aria",                   "Visualizza dettagli"),
        ("en", "mat.download_aria",                  "Download"),
        ("it", "mat.download_aria",                  "Scarica"),
        ("en", "mat.unlink_aria",                    "Unlink from lesson"),
        ("it", "mat.unlink_aria",                    "Rimuovi dalla lezione"),
        ("en", "mat.preview_btn",                    "Preview"),
        ("it", "mat.preview_btn",                    "Anteprima"),
        ("en", "mat.similar_titles_found",           "Similar titles found:"),
        ("it", "mat.similar_titles_found",           "Titoli simili trovati:"),
        ("en", "mat.student_readonly",               "You can view and download materials but not edit them."),
        ("it", "mat.student_readonly",               "Puoi visualizzare e scaricare i materiali ma non modificarli."),

        // admin – user management
        ("en", "admin.msg_user_created",             "User created."),
        ("it", "admin.msg_user_created",             "Utente creato."),
        ("en", "admin.msg_user_no_edit",             "You cannot edit your own role or status."),
        ("it", "admin.msg_user_no_edit",             "Non puoi modificare il tuo ruolo o stato."),
        ("en", "admin.msg_last_admin",               "Cannot deactivate the last active administrator."),
        ("it", "admin.msg_last_admin",               "Non puoi disattivare l'ultimo amministratore attivo."),
        ("en", "admin.msg_user_updated",             "User updated."),
        ("it", "admin.msg_user_updated",             "Utente aggiornato."),
        ("en", "admin.msg_cannot_delete_own",        "You cannot delete your own account."),
        ("it", "admin.msg_cannot_delete_own",        "Non puoi eliminare il tuo account."),
        ("en", "admin.msg_teacher_has_courses",      "Cannot delete teacher \u00ab{0}\u00bb: they have {1} active course(s). Reassign or delete the courses first."),
        ("it", "admin.msg_teacher_has_courses",      "Impossibile eliminare il docente \u00ab{0}\u00bb: ha {1} corso/i attivo/i. Riassegna o elimina i corsi prima."),
        ("en", "admin.msg_user_deleted",             "User \u00ab{0}\u00bb deleted."),
        ("it", "admin.msg_user_deleted",             "Utente \u00ab{0}\u00bb eliminato."),
        ("en", "admin.msg_user_activated",           "User activated."),
        ("it", "admin.msg_user_activated",           "Utente attivato."),
        ("en", "admin.msg_user_deactivated",         "User deactivated."),
        ("it", "admin.msg_user_deactivated",         "Utente disattivato."),

        // admin – area management
        ("en", "admin.msg_area_invalid_name",        "Invalid area name."),
        ("it", "admin.msg_area_invalid_name",        "Nome area non valido."),
        ("en", "admin.msg_area_exists",              "An area named \u00ab{0}\u00bb already exists."),
        ("it", "admin.msg_area_exists",              "Esiste gi\u00e0 un\u2019area con nome \u00ab{0}\u00bb."),
        ("en", "admin.msg_area_created",             "Area \u00ab{0}\u00bb created."),
        ("it", "admin.msg_area_created",             "Area \u00ab{0}\u00bb creata."),
        ("en", "admin.msg_area_renamed",             "Area renamed."),
        ("it", "admin.msg_area_renamed",             "Area rinominata."),
        ("en", "admin.msg_area_in_use",              "The area is used by {0} material(s) and cannot be deleted."),
        ("it", "admin.msg_area_in_use",              "L\u2019area \u00e8 utilizzata da {0} materiale/i e non pu\u00f2 essere eliminata."),
        ("en", "admin.msg_area_deleted",             "Area deleted."),
        ("it", "admin.msg_area_deleted",             "Area eliminata."),

        // admin – platform management
        ("en", "admin.msg_platform_invalid_name",    "Invalid platform name."),
        ("it", "admin.msg_platform_invalid_name",    "Nome piattaforma non valido."),
        ("en", "admin.msg_platform_exists",          "A platform named \u00ab{0}\u00bb already exists."),
        ("it", "admin.msg_platform_exists",          "Esiste gi\u00e0 una piattaforma con nome \u00ab{0}\u00bb."),
        ("en", "admin.msg_platform_created",         "Platform \u00ab{0}\u00bb created."),
        ("it", "admin.msg_platform_created",         "Piattaforma \u00ab{0}\u00bb creata."),
        ("en", "admin.msg_platform_renamed",         "Platform renamed to \u00ab{0}\u00bb."),
        ("it", "admin.msg_platform_renamed",         "Piattaforma rinominata in \u00ab{0}\u00bb."),
        ("en", "admin.msg_platform_in_use",          "The platform is used by {0} material(s) and cannot be deleted."),
        ("it", "admin.msg_platform_in_use",          "La piattaforma \u00e8 utilizzata da {0} materiale/i e non pu\u00f2 essere eliminata."),
        ("en", "admin.msg_platform_deleted",         "Platform deleted."),
        ("it", "admin.msg_platform_deleted",         "Piattaforma eliminata."),

        // admin – email settings
        ("en", "admin.email.saved",                  "Email settings saved."),
        ("it", "admin.email.saved",                  "Impostazioni email salvate."),
        ("en", "admin.email.save_error",             "Error saving settings: {0}"),
        ("it", "admin.email.save_error",             "Errore salvataggio impostazioni: {0}"),
        ("en", "admin.email.test_no_recipient",      "Enter a recipient address."),
        ("it", "admin.email.test_no_recipient",      "Inserisci un indirizzo destinatario."),
        ("en", "admin.email.test_sent",              "Test email sent to {0}."),
        ("it", "admin.email.test_sent",              "Email di test inviata a {0}."),
        ("en", "admin.email.test_failed",            "Email send failed: {0}"),
        ("it", "admin.email.test_failed",            "Invio email fallito: {0}"),

        // admin – language / translations
        ("en", "admin.msg_lang_saved",               "Language settings saved."),
        ("it", "admin.msg_lang_saved",               "Impostazioni lingua salvate."),
        ("en", "admin.msg_fill_defaults",            "Filled {0} missing translation(s) with English defaults."),
        ("it", "admin.msg_fill_defaults",            "Completate {0} traduzione/i mancanti con i valori predefiniti inglesi."),
        ("en", "admin.msg_translations_saved",       "Key \u00ab{0}\u00bb saved."),
        ("it", "admin.msg_translations_saved",       "Chiave \u00ab{0}\u00bb salvata."),
        ("en", "admin.msg_key_deleted",              "Key \u00ab{0}\u00bb deleted."),
        ("it", "admin.msg_key_deleted",              "Chiave \u00ab{0}\u00bb eliminata."),

        // admin – role management
        ("en", "admin.msg_role_created",             "Role \u00ab{0}\u00bb created."),
        ("it", "admin.msg_role_created",             "Ruolo \u00ab{0}\u00bb creato."),
        ("en", "admin.msg_admin_role_protected",     "The Admin role cannot be modified."),
        ("it", "admin.msg_admin_role_protected",     "Il ruolo Admin non pu\u00f2 essere modificato."),
        ("en", "admin.role_updated",                 "Role \u00ab{0}\u00bb updated."),
        ("it", "admin.role_updated",                 "Ruolo \u00ab{0}\u00bb aggiornato."),
        ("en", "admin.msg_admin_role_protected_del", "The Admin role cannot be deleted."),
        ("it", "admin.msg_admin_role_protected_del", "Il ruolo Admin non pu\u00f2 essere eliminato."),
        ("en", "admin.msg_role_in_use",              "Role \u00ab{0}\u00bb has {1} user(s) assigned and cannot be deleted."),
        ("it", "admin.msg_role_in_use",              "Il ruolo \u00ab{0}\u00bb ha {1} utente/i assegnati e non pu\u00f2 essere eliminato."),
        ("en", "admin.msg_role_deleted",             "Role \u00ab{0}\u00bb deleted."),
        ("it", "admin.msg_role_deleted",             "Ruolo \u00ab{0}\u00bb eliminato."),

        // admin – document types
        ("en", "admin.msg_doctype_invalid",          "Invalid document type name."),
        ("it", "admin.msg_doctype_invalid",          "Nome tipo documento non valido."),
        ("en", "admin.msg_doctype_exists",           "A document type named \u00ab{0}\u00bb already exists."),
        ("it", "admin.msg_doctype_exists",           "Esiste gi\u00e0 un tipo documento con nome \u00ab{0}\u00bb."),
        ("en", "admin.msg_doctype_created",          "Document type \u00ab{0}\u00bb created."),
        ("it", "admin.msg_doctype_created",          "Tipo documento \u00ab{0}\u00bb creato."),
        ("en", "admin.msg_doctype_updated",          "Document type updated."),
        ("it", "admin.msg_doctype_updated",          "Tipo documento aggiornato."),
        ("en", "admin.msg_doctype_in_use",           "The document type is used by {0} material(s) and cannot be deleted."),
        ("it", "admin.msg_doctype_in_use",           "Il tipo documento \u00e8 utilizzato da {0} materiale/i e non pu\u00f2 essere eliminato."),
        ("en", "admin.msg_doctype_deleted",          "Document type deleted."),
        ("it", "admin.msg_doctype_deleted",          "Tipo documento eliminato."),

        // admin – platform features
        ("en", "admin.msg_timezone_updated",         "Timezone updated."),
        ("it", "admin.msg_timezone_updated",         "Fuso orario aggiornato."),
        ("en", "admin.msg_courses_enabled",          "Courses module enabled."),
        ("it", "admin.msg_courses_enabled",          "Modulo corsi abilitato."),
        ("en", "admin.msg_courses_disabled",         "Courses module disabled."),
        ("it", "admin.msg_courses_disabled",         "Modulo corsi disabilitato."),
        ("en", "admin.msg_materials_enabled",        "Materials module enabled."),
        ("it", "admin.msg_materials_enabled",        "Modulo materiali abilitato."),
        ("en", "admin.msg_materials_disabled",       "Materials module disabled."),
        ("it", "admin.msg_materials_disabled",       "Modulo materiali disabilitato."),

        // admin – database / scripts / logs
        ("en", "admin.prod_script_error",            "Error generating the production script."),
        ("it", "admin.prod_script_error",            "Errore nella generazione dello script di produzione."),
        ("en", "admin.prod_script_expired",          "Session expired. Regenerate the script."),
        ("it", "admin.prod_script_expired",          "Sessione scaduta. Rigenera lo script."),
        ("en", "admin.msg_logs_purged",              "{0} log record(s) deleted."),
        ("it", "admin.msg_logs_purged",              "{0} record di log eliminati."),

        // ── DataImport wizard ─────────────────────────────────────────────
        ("en", "dataimport.page_title",          "Import data from SQL Server"),
        ("it", "dataimport.page_title",          "Importa dati da SQL Server"),
        ("en", "dataimport.card_title",          "Import from SQL Server"),
        ("it", "dataimport.card_title",          "Importa da SQL Server"),
        ("en", "dataimport.card_desc",           "Import materials and folders from an external SQL Server database using a guided step-by-step wizard."),
        ("it", "dataimport.card_desc",           "Importa materiali e cartelle da un database SQL Server esterno con un wizard guidato passo-passo."),
        ("en", "dataimport.card_btn",            "Start wizard"),
        ("it", "dataimport.card_btn",            "Avvia wizard"),
        ("en", "dataimport.cancel",              "Cancel"),
        ("it", "dataimport.cancel",              "Annulla"),
        ("en", "dataimport.back_to_db",          "Back to Database"),
        ("it", "dataimport.back_to_db",          "Torna a Database"),

        // Step 1 – Connect
        ("en", "dataimport.connect_title",       "Connect to source database"),
        ("it", "dataimport.connect_title",       "Connessione al database sorgente"),
        ("en", "dataimport.connect_hint",        "The connection string is never saved to disk and remains session-only."),
        ("it", "dataimport.connect_hint",        "La stringa non viene mai salvata su disco — rimane solo in sessione."),
        ("en", "dataimport.conn_str_label",      "SQL Server connection string"),
        ("it", "dataimport.conn_str_label",      "Connection string SQL Server"),
        ("en", "dataimport.test_btn",            "Test & proceed"),
        ("it", "dataimport.test_btn",            "Testa e procedi"),

        // Step 2 – Tables
        ("en", "dataimport.tables_title",        "Select source table and destination"),
        ("it", "dataimport.tables_title",        "Scegli tabella sorgente e destinazione"),
        ("en", "dataimport.target_label",        "MySQL destination table"),
        ("it", "dataimport.target_label",        "Tabella di destinazione MySQL"),
        ("en", "dataimport.target_materials",    "materials (Materials archive)"),
        ("it", "dataimport.target_materials",    "materials (Archivio materiali)"),
        ("en", "dataimport.target_folders",      "material_folders (Material folders)"),
        ("it", "dataimport.target_folders",      "material_folders (Cartelle materiali)"),

        // Step 3 – Map
        ("en", "dataimport.map_title",           "Column mapping"),
        ("it", "dataimport.map_title",           "Mappatura colonne"),
        ("en", "dataimport.target_field",        "Target field"),
        ("it", "dataimport.target_field",        "Campo destinazione"),
        ("en", "dataimport.source_field",        "Source column"),
        ("it", "dataimport.source_field",        "Colonna sorgente"),
        ("en", "dataimport.transform",           "Transform"),
        ("it", "dataimport.transform",           "Trasformazione"),
        ("en", "dataimport.conflict_label",      "Duplicate policy"),
        ("it", "dataimport.conflict_label",      "Politica duplicati"),
        ("en", "dataimport.dry_run_btn",         "Preview (Dry Run)"),
        ("it", "dataimport.dry_run_btn",         "Anteprima (Dry Run)"),
        ("en", "dataimport.execute_btn",         "Import now"),
        ("it", "dataimport.execute_btn",         "Importa ora"),
        ("en", "dataimport.dry_run_badge",       "DRY RUN"),
        ("it", "dataimport.dry_run_badge",       "DRY RUN"),

        // Step 4 – Result
        ("en", "dataimport.result_title",        "Import result"),
        ("it", "dataimport.result_title",        "Risultato importazione"),
        ("en", "dataimport.source_rows",         "Source rows"),
        ("it", "dataimport.source_rows",         "Righe sorgente"),
        ("en", "dataimport.inserted",            "Inserted"),
        ("it", "dataimport.inserted",            "Inserite"),
        ("en", "dataimport.updated",             "Updated"),
        ("it", "dataimport.updated",             "Aggiornate"),
        ("en", "dataimport.skipped",             "Skipped"),
        ("it", "dataimport.skipped",             "Saltate"),
        ("en", "dataimport.errors",              "Errors"),
        ("it", "dataimport.errors",              "Errori"),
        ("en", "dataimport.preview",             "Data preview"),
        ("it", "dataimport.preview",             "Anteprima dati"),
    };
    foreach (var (lang, key, val) in seeds)
    {
        try { await tr.UpsertAsync(lang, key, val); }
        catch { /* non bloccare l'avvio se il DB non è raggiungibile */ }
    }
    // Invalida la cache in-memory così le nuove chiavi sono visibili subito
    try
    {
        var ts = scope.ServiceProvider.GetRequiredService<TranslationService>();
        ts.InvalidateCache();
    }
    catch { }
});

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
