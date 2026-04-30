-- 022: Production-script UI translation keys (4 languages)
-- Idempotent: INSERT IGNORE

INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
-- admin.prod_script_title
('en','admin.prod_script_title','Production script'),
('it','admin.prod_script_title','Script per produzione'),
('es','admin.prod_script_title','Script de producción'),
('de','admin.prod_script_title','Produktionsskript'),

-- admin.prod_script_desc
('en','admin.prod_script_desc','Generates a complete SQL file to install Didasco on an empty production database. The script includes: drift detection, CREATE TABLE for all application tables, Admin role seed and an administrator user with a temporary password. Tables ''documents'' and ''document_versions'' (removed by migration 008) are excluded.'),
('it','admin.prod_script_desc','Genera un file SQL completo per installare Didasco su un database di produzione vuoto. Lo script include: drift detection, CREATE TABLE per tutte le tabelle dell''applicazione, seed del ruolo Admin e di un utente amministratore con password temporanea. Le tabelle ''documents'' e ''document_versions'' (eliminate dalla migrazione 008) sono escluse.'),
('es','admin.prod_script_desc','Genera un archivo SQL completo para instalar Didasco en una base de datos de producción vacía. El script incluye: detección de deriva, CREATE TABLE para todas las tablas de la aplicación, seed del rol Admin y de un usuario administrador con contraseña temporal. Las tablas ''documents'' y ''document_versions'' (eliminadas por la migración 008) están excluidas.'),
('de','admin.prod_script_desc','Erzeugt eine vollständige SQL-Datei zur Installation von Didasco auf einer leeren Produktionsdatenbank. Das Skript enthält: Drift-Erkennung, CREATE TABLE für alle Anwendungstabellen, Seed für die Admin-Rolle und einen Administratorbenutzer mit temporärem Passwort. Die Tabellen ''documents'' und ''document_versions'' (durch Migration 008 gelöscht) sind ausgeschlossen.'),

-- admin.prod_script_include_translations
('en','admin.prod_script_include_translations','Include translation keys in the script'),
('it','admin.prod_script_include_translations','Includi chiavi di traduzione nello script'),
('es','admin.prod_script_include_translations','Incluir claves de traducción en el script'),
('de','admin.prod_script_include_translations','Übersetzungsschlüssel in das Skript einbeziehen'),

-- admin.prod_script_translations_hint
('en','admin.prod_script_translations_hint','If checked, inserts all translations from the database (INSERT … ON DUPLICATE KEY UPDATE).'),
('it','admin.prod_script_translations_hint','Se selezionato, inserisce tutte le traduzioni presenti nel DB (INSERT … ON DUPLICATE KEY UPDATE).'),
('es','admin.prod_script_translations_hint','Si se activa, inserta todas las traducciones de la base de datos (INSERT … ON DUPLICATE KEY UPDATE).'),
('de','admin.prod_script_translations_hint','Wenn aktiviert, werden alle Übersetzungen aus der Datenbank eingefügt (INSERT … ON DUPLICATE KEY UPDATE).'),

-- admin.prod_script_generate_btn
('en','admin.prod_script_generate_btn','Generate script'),
('it','admin.prod_script_generate_btn','Genera script'),
('es','admin.prod_script_generate_btn','Generar script'),
('de','admin.prod_script_generate_btn','Skript generieren'),

-- admin.prod_script_ready
('en','admin.prod_script_ready','Script generated — temporary admin password'),
('it','admin.prod_script_ready','Script generato — password temporanea admin'),
('es','admin.prod_script_ready','Script generado — contraseña temporal de administrador'),
('de','admin.prod_script_ready','Skript generiert — temporäres Admin-Passwort'),

-- admin.prod_script_pwd_hint
('en','admin.prod_script_pwd_hint','This password is shown only once. Note it down before closing the page. The download starts automatically.'),
('it','admin.prod_script_pwd_hint','Questa password verrà mostrata una sola volta. Annotarla prima di chiudere la pagina. Il download si avvia in automatico.'),
('es','admin.prod_script_pwd_hint','Esta contraseña se muestra una sola vez. Anótela antes de cerrar la página. La descarga se inicia automáticamente.'),
('de','admin.prod_script_pwd_hint','Dieses Passwort wird nur einmal angezeigt. Notieren Sie es, bevor Sie die Seite schließen. Der Download startet automatisch.'),

-- admin.prod_script_download
('en','admin.prod_script_download','Download SQL script'),
('it','admin.prod_script_download','Scarica script SQL'),
('es','admin.prod_script_download','Descargar script SQL'),
('de','admin.prod_script_download','SQL-Skript herunterladen'),

-- admin.prod_script_auto_download
('en','admin.prod_script_auto_download','(download starts automatically)'),
('it','admin.prod_script_auto_download','(il download si avvia automaticamente)'),
('es','admin.prod_script_auto_download','(la descarga se inicia automáticamente)'),
('de','admin.prod_script_auto_download','(Download startet automatisch)'),

-- admin.prod_script_already_ready
('en','admin.prod_script_already_ready','A script is still available from the previous page load.'),
('it','admin.prod_script_already_ready','Uno script è ancora disponibile dal caricamento precedente.'),
('es','admin.prod_script_already_ready','Hay un script disponible de la carga de página anterior.'),
('de','admin.prod_script_already_ready','Ein Skript aus dem vorherigen Seitenaufruf ist noch verfügbar.'),

-- admin.prod_script_warning_title
('en','admin.prod_script_warning_title','Notes'),
('it','admin.prod_script_warning_title','Avvertenze'),
('es','admin.prod_script_warning_title','Notas'),
('de','admin.prod_script_warning_title','Hinweise'),

-- admin.prod_script_w1
('en','admin.prod_script_w1','The temporary password is shown only once. Note it down before closing the page.'),
('it','admin.prod_script_w1','La password temporanea viene mostrata una sola volta al momento del download. Annotarla prima di chiudere la pagina.'),
('es','admin.prod_script_w1','La contraseña temporal se muestra una sola vez. Anótela antes de cerrar la página.'),
('de','admin.prod_script_w1','Das temporäre Passwort wird nur einmal angezeigt. Notieren Sie es, bevor Sie die Seite schließen.'),

-- admin.prod_script_w2
('en','admin.prod_script_w2','The script does NOT delete existing data: it uses CREATE TABLE IF NOT EXISTS and INSERT IGNORE.'),
('it','admin.prod_script_w2','Lo script NON cancella dati esistenti: usa CREATE TABLE IF NOT EXISTS e INSERT IGNORE.'),
('es','admin.prod_script_w2','El script NO elimina datos existentes: usa CREATE TABLE IF NOT EXISTS e INSERT IGNORE.'),
('de','admin.prod_script_w2','Das Skript LÖSCHT KEINE vorhandenen Daten: es verwendet CREATE TABLE IF NOT EXISTS und INSERT IGNORE.'),

-- admin.prod_script_w3
('en','admin.prod_script_w3','The drift detection block aborts the script if it finds tables with different column names or order.'),
('it','admin.prod_script_w3','Il blocco di drift detection interrompe lo script se trova tabelle con struttura e nomi colonne diversi da quelli attesi.'),
('es','admin.prod_script_w3','El bloque de detección de deriva aborta el script si encuentra tablas con nombres de columnas u orden diferentes.'),
('de','admin.prod_script_w3','Der Drift-Erkennungsblock bricht das Skript ab, wenn er Tabellen mit abweichenden Spaltennamen oder -reihenfolgen findet.'),

-- admin.prod_script_w4
('en','admin.prod_script_w4','Run with: mysql -u<user> -p <database> < didasco_production_YYYYMMDD.sql'),
('it','admin.prod_script_w4','Eseguire con: mysql -u<user> -p <database> < didasco_production_YYYYMMDD.sql'),
('es','admin.prod_script_w4','Ejecutar con: mysql -u<user> -p <database> < didasco_production_YYYYMMDD.sql'),
('de','admin.prod_script_w4','Ausführen mit: mysql -u<user> -p <database> < didasco_production_YYYYMMDD.sql'),

-- admin.prod_script_error
('en','admin.prod_script_error','Error generating the script: '),
('it','admin.prod_script_error','Errore durante la generazione dello script: '),
('es','admin.prod_script_error','Error al generar el script: '),
('de','admin.prod_script_error','Fehler beim Generieren des Skripts: '),

-- admin.prod_script_expired
('en','admin.prod_script_expired','Script no longer available. Please regenerate.'),
('it','admin.prod_script_expired','Script non più disponibile. Rigenerare.'),
('es','admin.prod_script_expired','Script no disponible. Vuelva a generarlo.'),
('de','admin.prod_script_expired','Skript nicht mehr verfügbar. Bitte neu generieren.'),

-- common.copy (if not already present)
('en','common.copy','Copy'),
('it','common.copy','Copia'),
('es','common.copy','Copiar'),
('de','common.copy','Kopieren');
