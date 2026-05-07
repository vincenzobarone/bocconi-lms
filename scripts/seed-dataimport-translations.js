#!/usr/bin/env node
/**
 * Seed script — DataImport wizard translation keys (EN + IT)
 * Run from repo root:
 *   node artifacts/bocconi-lms/scripts/seed-dataimport-translations.js
 *
 * 29 keys × 2 languages = 58 rows (upsert — idempotent)
 */

const mysql = require('mysql2/promise');

const cs  = process.env.MYSQL_CONNECTION_STRING ?? '';
const get = k => { const m = cs.match(new RegExp(k + '=([^;]+)', 'i')); return m ? m[1] : ''; };

const translations = [
  // ── Page / common ────────────────────────────────────────────────────
  { key: 'dataimport.page_title',
    en: 'Import data from SQL Server',
    it: 'Importa dati da SQL Server' },
  { key: 'dataimport.cancel',
    en: 'Cancel',
    it: 'Annulla' },
  { key: 'dataimport.back_to_db',
    en: 'Back to Database',
    it: 'Torna a Database' },

  // ── Step 1: Connect ──────────────────────────────────────────────────
  { key: 'dataimport.connect_title',
    en: 'Source connection',
    it: 'Connessione sorgente' },
  { key: 'dataimport.connect_hint',
    en: 'The connection string is stored only in the current session and is never written to the database or logs.',
    it: 'La connection string viene conservata solo nella sessione corrente e non viene mai scritta nel database o nei log.' },
  { key: 'dataimport.conn_str_label',
    en: 'SQL Server connection string',
    it: 'Connection string SQL Server' },
  { key: 'dataimport.test_btn',
    en: 'Test connection',
    it: 'Testa connessione' },

  // ── Step 2: Tables ───────────────────────────────────────────────────
  { key: 'dataimport.tables_title',
    en: 'Select source table',
    it: 'Seleziona tabella sorgente' },
  { key: 'dataimport.target_label',
    en: 'Import target',
    it: 'Destinazione importazione' },
  { key: 'dataimport.target_materials',
    en: 'Materials',
    it: 'Materiali' },
  { key: 'dataimport.target_folders',
    en: 'Material folders',
    it: 'Cartelle materiali' },

  // ── Step 3: Mapping ──────────────────────────────────────────────────
  { key: 'dataimport.map_title',
    en: 'Column mapping',
    it: 'Mappatura colonne' },
  { key: 'dataimport.target_field',
    en: 'Target field',
    it: 'Campo destinazione' },
  { key: 'dataimport.source_field',
    en: 'Source column',
    it: 'Colonna sorgente' },
  { key: 'dataimport.transform',
    en: 'Transform',
    it: 'Trasformazione' },
  { key: 'dataimport.conflict_label',
    en: 'On conflict (duplicate)',
    it: 'In caso di conflitto (duplicato)' },
  { key: 'dataimport.dry_run_btn',
    en: 'Dry run (simulate)',
    it: 'Simulazione (dry run)' },
  { key: 'dataimport.execute_btn',
    en: 'Execute import',
    it: 'Esegui importazione' },
  { key: 'dataimport.dry_run_badge',
    en: 'DRY RUN',
    it: 'SIMULAZIONE' },

  // ── Step 4: Result ───────────────────────────────────────────────────
  { key: 'dataimport.result_title',
    en: 'Import result',
    it: 'Risultato importazione' },
  { key: 'dataimport.source_rows',
    en: 'Source rows',
    it: 'Righe sorgente' },
  { key: 'dataimport.inserted',
    en: 'Inserted',
    it: 'Inseriti' },
  { key: 'dataimport.updated',
    en: 'Updated',
    it: 'Aggiornati' },
  { key: 'dataimport.skipped',
    en: 'Skipped',
    it: 'Saltati' },
  { key: 'dataimport.errors',
    en: 'Errors',
    it: 'Errori' },
  { key: 'dataimport.preview',
    en: 'Preview (first 20 rows)',
    it: 'Anteprima (prime 20 righe)' },

  // ── Database.cshtml card ─────────────────────────────────────────────
  { key: 'dataimport.card_title',
    en: 'Import data from external SQL Server',
    it: 'Importa dati da SQL Server esterno' },
  { key: 'dataimport.card_desc',
    en: 'Connect to an external SQL Server source and import rows into the materials or material_folders tables. One-shot wizard, connection string stored only in session.',
    it: 'Connettiti a un SQL Server esterno e importa righe nelle tabelle materials o material_folders. Wizard one-shot, la connection string è conservata solo in sessione.' },
  { key: 'dataimport.card_btn',
    en: 'Start import wizard',
    it: 'Avvia wizard importazione' },
];

(async () => {
  const conn = await mysql.createConnection({
    host:     get('server'),
    port:     parseInt(get('port') || '3306'),
    user:     get('user id') || get('uid') || get('user'),
    password: get('password') || get('pwd'),
    database: get('database'),
  });

  let inserted = 0;
  let skipped  = 0;

  for (const { key, en, it } of translations) {
    for (const [lang, val] of [['en', en], ['it', it]]) {
      await conn.execute(
        `INSERT INTO translations (language_code, label_key, label_value, created_at)
         VALUES (?, ?, ?, NOW())
         ON DUPLICATE KEY UPDATE label_value = VALUES(label_value), updated_at = NOW()`,
        [lang, key, val]
      );
      inserted++;
    }
  }

  console.log(`DataImport translations seed: upserted ${inserted} rows (${translations.length} keys × 2 languages)`);
  await conn.end();
})().catch(err => { console.error(err.message); process.exit(1); });
