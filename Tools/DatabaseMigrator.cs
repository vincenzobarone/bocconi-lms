using MySqlConnector;

namespace BocconiLMS.Tools;

/// <summary>
/// Versioned, idempotent schema-and-seed migrator.
/// Each migration runs exactly once; applied IDs are tracked in `schema_migrations`.
/// Safe to call on every app start.
/// </summary>
public static class DatabaseMigrator
{
    // ── Migration list ────────────────────────────────────────────────────────
    // Each entry: (id, description, sql[])
    // SQL statements are executed in order, each in its own command.
    // Use IF NOT EXISTS / INSERT IGNORE / INSERT … ON DUPLICATE KEY for idempotency.
    private static readonly (string id, string desc, string[] sql)[] Migrations =
    [
        // ── M001: authors table ───────────────────────────────────────────────
        ("M001", "Create authors table",
        [
            """
            CREATE TABLE IF NOT EXISTS authors (
                id          INT AUTO_INCREMENT PRIMARY KEY,
                full_name   VARCHAR(255) NOT NULL,
                email       VARCHAR(255) NULL,
                affiliation VARCHAR(255) NULL,
                created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UNIQUE KEY uk_full_name (full_name),
                INDEX idx_email (email)
            ) ENGINE=InnoDB
            """
        ]),

        // ── M002: material_authors join table ─────────────────────────────────
        ("M002", "Create material_authors join table",
        [
            """
            CREATE TABLE IF NOT EXISTS material_authors (
                material_id INT NOT NULL,
                author_id   INT NOT NULL,
                sort_order  INT NOT NULL DEFAULT 0,
                PRIMARY KEY (material_id, author_id),
                FOREIGN KEY (material_id) REFERENCES materials(id) ON DELETE CASCADE,
                FOREIGN KEY (author_id)   REFERENCES authors(id)   ON DELETE CASCADE,
                INDEX idx_author   (author_id),
                INDEX idx_material (material_id)
            ) ENGINE=InnoDB
            """
        ]),

        // ── M003: backfill legacy author_name → authors + material_authors ────
        // SQL is built dynamically in RunAsync because the column may not exist.
        ("M003", "Backfill author_name into authors/material_authors",
        [
            "__DYNAMIC_M003__"
        ]),

        // ── M004: author translation keys ─────────────────────────────────────
        ("M004", "Seed author.* translation keys",
        [
            BuildTranslationInserts([
                ("author.title",                  "Autori",                                         "Authors"),
                ("author.btn_new",                "Nuovo autore",                                   "New author"),
                ("author.create_title",           "Nuovo autore",                                   "Create author"),
                ("author.edit_title",             "Modifica autore",                                "Edit author"),
                ("author.create_btn",             "Crea",                                           "Create"),
                ("author.col_name",               "Nome",                                           "Name"),
                ("author.col_email",              "Email",                                          "Email"),
                ("author.col_affiliation",        "Affiliazione",                                   "Affiliation"),
                ("author.col_materials",          "Materiali",                                      "Materials"),
                ("author.name_placeholder",       "Nome Cognome",                                   "First Last"),
                ("author.affiliation_placeholder","es. Dipartimento di Economia",                   "e.g. Economics Dept"),
                ("author.name_duplicate",         "Esiste già un autore con questo nome",           "An author with this name already exists"),
                ("author.msg_created",            "Autore creato con successo",                     "Author created successfully"),
                ("author.msg_updated",            "Autore aggiornato",                              "Author updated"),
                ("author.msg_deleted",            "Autore eliminato",                               "Author deleted"),
                ("author.msg_delete_blocked",     "Impossibile eliminare: autore collegato a materiali", "Cannot delete: author linked to materials"),
                ("author.no_results",             "Nessun autore trovato.",                         "No authors found."),
                ("author.create_first",           "Crea il primo",                                  "Create the first one"),
                ("author.delete_blocked_hint",    "Non eliminabile: collegato a materiali",         "Cannot delete: linked to materials"),
                ("author.linked_to_n_materials",  "Autore collegato a {0} materiale/i",             "Author linked to {0} material(s)"),
            ])
        ]),

        // ── M005: material author-widget translation keys ──────────────────────
        ("M005", "Seed mat.author_* translation keys",
        [
            BuildTranslationInserts([
                ("mat.author_multiselect_hint",   "Tieni Ctrl premuto per selezionare più autori",       "Hold Ctrl to select multiple authors"),
                ("mat.authors_placeholder",       "Cerca autore…",                                       "Search author…"),
                ("mat.authors_hint",              "Digita per cercare · trascina i badge per riordinare","Type to search · drag badges to reorder"),
                ("mat.label_authors",             "Autori",                                              "Authors"),
            ])
        ]),

        // ── M007: rename translation keys to canonical names ──────────────────
        ("M007", "Rename translation keys to canonical names",
        [
            "UPDATE translations SET label_key='author.title'         WHERE label_key='author.page_title'",
            "UPDATE translations SET label_key='author.btn_new'       WHERE label_key='author.new_btn'",
            "UPDATE translations SET label_key='mat.label_authors'    WHERE label_key='mat.label_author'",
            "UPDATE translations SET label_key='mat.authors_placeholder' WHERE label_key='mat.author_add_placeholder'",
            "UPDATE translations SET label_key='mat.authors_hint'     WHERE label_key='mat.author_tag_hint'",
            // Ensure new keys exist (idempotent for fresh installs where M004/M005 already used new names)
            BuildTranslationInserts([
                ("author.title",            "Autori",    "Authors"),
                ("author.btn_new",          "Nuovo autore", "New author"),
                ("mat.label_authors",       "Autori",    "Authors"),
                ("mat.authors_placeholder", "Cerca autore…", "Search author…"),
                ("mat.authors_hint",        "Digita per cercare · trascina i badge per riordinare", "Type to search · drag badges to reorder"),
            ]),
        ]),

        // ── M008: drop legacy author_name column (dynamic — guarded in C#) ────
        ("M008", "Drop materials.author_name legacy column",
        [
            "__DYNAMIC_M008__"
        ]),

        // ── M009: add author.warn_in_use key (missed in M004) ────────────────
        ("M009", "Add author.warn_in_use translation key",
        [
            BuildTranslationInserts([
                ("author.warn_in_use", "Autore collegato a materiali — non eliminabile", "Author linked to materials — cannot delete"),
            ])
        ]),

        // ── M006: DataImport wizard translation keys ───────────────────────────
        ("M006", "Seed dataimport.* translation keys",
        [
            BuildTranslationInserts([
                ("dataimport.page_title",   "Importa dati da SQL Server",                    "Import data from SQL Server"),
                ("dataimport.cancel",       "Annulla",                                        "Cancel"),
                ("dataimport.connect_title","Connessione al database sorgente",               "Connect to source database"),
                ("dataimport.connect_hint", "La connection string è usata solo in questa sessione e non viene mai salvata su disco.", "The connection string is used only in this session and is never persisted."),
                ("dataimport.conn_str_label","Connection String SQL Server",                  "SQL Server Connection String"),
                ("dataimport.test_btn",     "Testa e connetti",                               "Test & connect"),
                ("dataimport.tables_title", "Selezione tabella sorgente",                     "Select source table"),
                ("dataimport.target_label", "Destinazione importazione",                      "Import target"),
                ("dataimport.target_materials","Materiali didattici",                         "Teaching materials"),
                ("dataimport.target_folders","Cartelle",                                      "Folders"),
                ("dataimport.map_title",    "Configurazione mapping colonne",                 "Column mapping"),
                ("dataimport.dryrun_btn",   "Anteprima (dry run)",                            "Preview (dry run)"),
                ("dataimport.execute_btn",  "Importa definitivamente",                        "Import for real"),
                ("dataimport.result_title", "Risultato importazione",                         "Import result"),
                ("dataimport.card_title",   "Importa dati da SQL Server",                     "Import data from SQL Server"),
                ("dataimport.card_desc",    "Importa materiali e cartelle da un DB SQL Server esterno con mappatura colonne e audit completo.", "Import materials and folders from an external SQL Server DB with column mapping and full audit."),
                ("dataimport.card_btn",     "Avvia procedura guidata",                        "Start wizard"),
                ("dataimport.step_connect", "Connessione",                                    "Connect"),
                ("dataimport.step_tables",  "Tabella",                                        "Table"),
                ("dataimport.step_map",     "Mapping",                                        "Mapping"),
                ("dataimport.step_result",  "Risultato",                                      "Result"),
            ])
        ]),

        // ── M011: mat.authors_browse key ─────────────────────────────────────
        ("M011", "Add mat.authors_browse translation key",
        [
            BuildTranslationInserts([
                ("mat.authors_browse",     "Sfoglia autori",    "Browse authors"),
            ])
        ]),

        // ── M014: course_code column + translation keys ───────────────────────
        ("M014", "Add course_code column to materials and translation keys",
        [
            "ALTER TABLE materials ADD COLUMN course_code VARCHAR(100) NULL AFTER external_link",
            BuildTranslationInserts([
                ("mat.label_course_code", "Codice corso", "Course code"),
                ("mat.help_course_code",
                 "Il codice del corso dovrebbe essere CM/OM_ANNO_CODICE SAP per corsi open/custom, per corsi master M1ANNOCODICESAP o caricato (non obbligatorio)",
                 "The course code should be CM/OM_YEAR_SAP CODE for open/custom courses, for master courses M1YEARSAPCODE or uploaded (optional)"),
            ])
        ]),

        // ── M013: last_update column + translation key (dynamic: checks column exists)
        ("M013", "Add last_update column to materials and translation key",
        ["__DYNAMIC_M013__"]),

        // ── M012: modal autori — placeholder keys ────────────────────────────
        ("M012", "Add mat.author_new_placeholder and mat.author_filter_placeholder keys",
        [
            BuildTranslationInserts([
                ("mat.author_new_placeholder",    "Nuovo autore…",  "New author…"),
                ("mat.author_filter_placeholder", "Filtra…",        "Filter…"),
            ])
        ]),

        // ── M015: protocol_code rename + old_protocol column ─────────────────
        ("M015", "Rename protocol_number to protocol_code and add old_protocol",
        ["__DYNAMIC_M015__"]),

        // ── M010: DataImport wizard — chiavi mancanti da M006 ─────────────────
        ("M010", "Add missing dataimport.* translation keys",
        [
            BuildTranslationInserts([
                ("dataimport.dry_run_btn",    "Anteprima (dry run)",                          "Preview (dry run)"),
                ("dataimport.dry_run_badge",  "Simulazione",                                  "Dry run"),
                ("dataimport.back_to_db",     "Torna al database",                            "Back to database"),
                ("dataimport.conflict_label", "Gestione duplicati",                           "Duplicate handling"),
                ("dataimport.source_rows",    "Righe sorgente",                               "Source rows"),
                ("dataimport.inserted",       "Inseriti",                                     "Inserted"),
                ("dataimport.updated",        "Aggiornati",                                   "Updated"),
                ("dataimport.skipped",        "Saltati",                                      "Skipped"),
                ("dataimport.errors",         "Errori",                                       "Errors"),
                ("dataimport.target_field",   "Campo destinazione",                           "Target field"),
                ("dataimport.source_field",   "Campo sorgente",                               "Source field"),
                ("dataimport.transform",      "Trasformazione",                               "Transform"),
                ("dataimport.preview",        "Anteprima righe elaborate",                    "Processed rows preview"),
            ])
        ]),
    ];

    // ── Public entry point ────────────────────────────────────────────────────

    public static async Task RunAsync(string connectionString, ILogger logger)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        await EnsureMigrationsTableAsync(conn);

        foreach (var (id, desc, statements) in Migrations)
        {
            if (await IsAppliedAsync(conn, id)) continue;

            logger.LogInformation("[Migration] Applying {Id}: {Desc}", id, desc);

            if (statements is ["__DYNAMIC_M003__"])
            {
                await ApplyDynamicM003Async(conn, logger);
            }
            else if (statements is ["__DYNAMIC_M008__"])
            {
                await ApplyDynamicM008Async(conn, logger);
            }
            else if (statements is ["__DYNAMIC_M013__"])
            {
                await ApplyDynamicM013Async(conn, logger);
            }
            else if (statements is ["__DYNAMIC_M015__"])
            {
                await ApplyDynamicM015Async(conn, logger);
            }
            else
            {
                foreach (var sql in statements)
                {
                    await using var cmd = new MySqlCommand(sql, conn);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await MarkAppliedAsync(conn, id, desc);
            logger.LogInformation("[Migration] Applied  {Id}", id);
        }
    }

    /// <summary>
    /// M003: backfills legacy materials.author_name only if the column still exists.
    /// Column existence is checked in C# before building any SQL, because MySQL
    /// validates column names at parse-time — a WHERE subquery cannot bypass that.
    /// </summary>
    private static async Task ApplyDynamicM003Async(MySqlConnection conn, ILogger logger)
    {
        // 1. Check whether author_name column still exists
        await using var check = new MySqlCommand("""
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name   = 'materials'
              AND column_name  = 'author_name'
            """, conn);
        var colExists = Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;

        if (!colExists)
        {
            logger.LogInformation("[Migration] M003: author_name column not found — no backfill needed");
            return;
        }

        // 2. Copy distinct names into authors
        await using var ins1 = new MySqlCommand("""
            INSERT IGNORE INTO authors (full_name)
            SELECT DISTINCT TRIM(author_name)
            FROM   materials
            WHERE  author_name IS NOT NULL AND TRIM(author_name) <> ''
            """, conn);
        var rows1 = await ins1.ExecuteNonQueryAsync();

        // 3. Create bridge rows
        await using var ins2 = new MySqlCommand("""
            INSERT IGNORE INTO material_authors (material_id, author_id, sort_order)
            SELECT m.id, a.id, 0
            FROM   materials m
            JOIN   authors   a ON a.full_name = TRIM(m.author_name)
            WHERE  m.author_name IS NOT NULL AND TRIM(m.author_name) <> ''
            """, conn);
        var rows2 = await ins2.ExecuteNonQueryAsync();

        logger.LogInformation("[Migration] M003: backfilled {Authors} authors, {Links} material_authors links",
            rows1, rows2);
    }

    /// <summary>
    /// M008: drops materials.author_name only if the column still exists.
    /// </summary>
    private static async Task ApplyDynamicM008Async(MySqlConnection conn, ILogger logger)
    {
        await using var check = new MySqlCommand("""
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name   = 'materials'
              AND column_name  = 'author_name'
            """, conn);
        var colExists = Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;

        if (!colExists)
        {
            logger.LogInformation("[Migration] M008: author_name column not found — skip DROP");
            return;
        }

        await using var drop = new MySqlCommand(
            "ALTER TABLE materials DROP COLUMN author_name", conn);
        await drop.ExecuteNonQueryAsync();
        logger.LogInformation("[Migration] M008: dropped materials.author_name");
    }

    /// <summary>
    /// M013: adds last_update column only if it does not already exist,
    /// then inserts the translation key (idempotent via ON DUPLICATE KEY).
    /// </summary>
    private static async Task ApplyDynamicM013Async(MySqlConnection conn, ILogger logger)
    {
        await using var check = new MySqlCommand("""
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name   = 'materials'
              AND column_name  = 'last_update'
            """, conn);
        var colExists = Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;

        if (!colExists)
        {
            await using var alter = new MySqlCommand(
                "ALTER TABLE materials ADD COLUMN last_update DATE NULL AFTER catalogation_date", conn);
            await alter.ExecuteNonQueryAsync();
            logger.LogInformation("[Migration] M013: added last_update column");
        }
        else
        {
            logger.LogInformation("[Migration] M013: last_update column already present — skip ALTER");
        }

        await using var tr = new MySqlCommand(
            BuildTranslationInserts([
                ("mat.label_last_update", "Data ultimo aggiornamento", "Last update date"),
            ]), conn);
        await tr.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// M015: renames protocol_number → protocol_code (VARCHAR 100) and adds old_protocol (VARCHAR 200).
    /// Both operations are guarded via information_schema because MySQL does not support
    /// ADD/CHANGE IF NOT EXISTS/IF EXISTS syntax on this server version.
    /// </summary>
    private static async Task ApplyDynamicM015Async(MySqlConnection conn, ILogger logger)
    {
        // 1. Rename protocol_number → protocol_code (only if protocol_number still exists)
        await using var checkOld = new MySqlCommand("""
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name   = 'materials'
              AND column_name  = 'protocol_number'
            """, conn);
        var oldExists = Convert.ToInt32(await checkOld.ExecuteScalarAsync()) > 0;

        if (oldExists)
        {
            await using var rename = new MySqlCommand(
                "ALTER TABLE materials CHANGE COLUMN protocol_number protocol_code VARCHAR(100) NULL", conn);
            await rename.ExecuteNonQueryAsync();
            logger.LogInformation("[Migration] M015: renamed protocol_number → protocol_code VARCHAR(100)");
        }
        else
        {
            // Ensure protocol_code column exists (fresh install skips the rename)
            await using var checkNew = new MySqlCommand("""
                SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE()
                  AND table_name   = 'materials'
                  AND column_name  = 'protocol_code'
                """, conn);
            var newExists = Convert.ToInt32(await checkNew.ExecuteScalarAsync()) > 0;
            if (!newExists)
            {
                await using var addCode = new MySqlCommand(
                    "ALTER TABLE materials ADD COLUMN protocol_code VARCHAR(100) NULL", conn);
                await addCode.ExecuteNonQueryAsync();
                logger.LogInformation("[Migration] M015: added protocol_code column (fresh install)");
            }
            else
            {
                logger.LogInformation("[Migration] M015: protocol_code already present — skip rename");
            }
        }

        // 2. Add old_protocol column if missing
        await using var checkOp = new MySqlCommand("""
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name   = 'materials'
              AND column_name  = 'old_protocol'
            """, conn);
        var opExists = Convert.ToInt32(await checkOp.ExecuteScalarAsync()) > 0;

        if (!opExists)
        {
            await using var addOp = new MySqlCommand(
                "ALTER TABLE materials ADD COLUMN old_protocol VARCHAR(200) NULL AFTER protocol_code", conn);
            await addOp.ExecuteNonQueryAsync();
            logger.LogInformation("[Migration] M015: added old_protocol column");
        }
        else
        {
            logger.LogInformation("[Migration] M015: old_protocol already present — skip ADD");
        }

        // 3. Translation keys (idempotent via ON DUPLICATE KEY)
        await using var tr = new MySqlCommand(
            BuildTranslationInserts([
                ("mat.protocol_code", "Codice protocollo", "Protocol code"),
                ("mat.old_protocol",  "Protocollo originale", "Original protocol"),
            ]), conn);
        await tr.ExecuteNonQueryAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task EnsureMigrationsTableAsync(MySqlConnection conn)
    {
        await using var cmd = new MySqlCommand("""
            CREATE TABLE IF NOT EXISTS schema_migrations (
                id          VARCHAR(20) NOT NULL PRIMARY KEY,
                description VARCHAR(255) NOT NULL,
                applied_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            ) ENGINE=InnoDB
            """, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<bool> IsAppliedAsync(MySqlConnection conn, string id)
    {
        await using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM schema_migrations WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static async Task MarkAppliedAsync(MySqlConnection conn, string id, string desc)
    {
        await using var cmd = new MySqlCommand(
            "INSERT IGNORE INTO schema_migrations (id, description) VALUES (@id, @desc)", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@desc", desc);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Builds a single INSERT … ON DUPLICATE KEY UPDATE statement for all
    /// (key, IT, EN) triples so the whole migration step is one SQL command.
    /// </summary>
    private static string BuildTranslationInserts(
        (string key, string it, string en)[] rows)
    {
        var values = new System.Text.StringBuilder();
        bool first = true;
        foreach (var (key, it, en) in rows)
        {
            foreach (var (lang, val) in new[] { ("IT", it), ("EN", en) })
            {
                if (!first) values.Append(',');
                first = false;
                var escapedVal = val.Replace("'", "''");
                values.Append($"('{lang}','{key}','{escapedVal}')");
            }
        }
        return $"""
            INSERT INTO translations (language_code, label_key, label_value)
            VALUES {values}
            ON DUPLICATE KEY UPDATE label_value = VALUES(label_value)
            """;
    }
}
