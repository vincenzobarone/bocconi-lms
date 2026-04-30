-- 017: Fix/correct existing translation values via ON DUPLICATE KEY UPDATE
-- These override stale INSERT IGNORE values from earlier migrations.

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
    ('de','mat.upload_optional','Neue Versionsdatei hochladen (optional)'),
    ('en','nav.users','Users'),
    ('it','nav.users','Utenti'),
    ('es','nav.users','Usuarios'),
    ('de','nav.users','Benutzer'),
    ('it','mat.protocol_number','Numero di protocollo'),
    ('en','mat.protocol_number','Protocol number'),
    ('it','mat.protocol_auto','Assegnato automaticamente al salvataggio'),
    ('en','mat.protocol_auto','Assigned automatically on save'),
    ('it','mat.verified_modal_title','Verifica completata'),
    ('en','mat.verified_modal_title','Verification complete'),
    ('it','mat.verified_modal_hint','Completa i dati di registrazione prima di salvare come Verificato.'),
    ('en','mat.verified_modal_hint','Complete the registration data before saving as Verified.'),
    ('it','mat.label_folder','Cartella'),
    ('en','mat.label_folder','Folder'),
    ('it','mat.folder_hint','Raggruppamento logico del documento. Il prefisso lingua (IT, EN…) viene aggiunto automaticamente.'),
    ('en','mat.folder_hint','Logical grouping of the document. The language prefix (IT, EN…) is added automatically.'),
    ('it','mat.folder_required','Seleziona una cartella o inserisci il nome della nuova.'),
    ('en','mat.folder_required','Select a folder or enter the name of the new one.'),
    ('it','mat.folder_filter','Filtra cartelle…'),
    ('en','mat.folder_filter','Filter folders…'),
    ('it','mat.folder_new','+ Nuova cartella'),
    ('en','mat.folder_new','+ New folder'),
    ('it','mat.folder_new_placeholder','es. DOCUMENTI VARI'),
    ('en','mat.folder_new_placeholder','e.g. MISC DOCUMENTS'),
    ('it','mat.modal_confirm','Conferma e salva'),
    ('en','mat.modal_confirm','Confirm and save'),
    ('it','common.cancel','Annulla'),
    ('en','common.cancel','Cancel'),
    ('en','perm.menu_translations','Dictionary — section access'),
    ('it','perm.menu_translations','Dictionary — accesso sezione'),
    ('es','perm.menu_translations','Dictionary — acceso a sección'),
    ('de','perm.menu_translations','Dictionary — Bereichszugang'),
    ('en','admin.edit_role','Edit role'),
    ('it','admin.edit_role','Modifica ruolo'),
    ('en','admin.platforms_tab','Platforms'),
    ('en','admin.platform_add','Add platform'),
    ('en','admin.platform_name','Platform name'),
    ('en','admin.create_platform','Create platform'),
    ('en','admin.edit_platform','Edit platform'),
    ('en','admin.delete_platform','Delete platform'),
    ('en','admin.delete_platform_confirm','Delete platform'),
    ('en','admin.no_platforms','No platforms defined yet.'),
    ('en','admin.platform_name_placeholder','Platform name…'),
    ('en','admin.configure','Configure'),
    ('it','admin.configure','Configura'),
    ('es','admin.configure','Configurar'),
    ('de','admin.configure','Konfigurieren'),
    ('en','admin.role_updated','Role updated to ''{0}''.'),
    ('it','admin.role_updated','Ruolo aggiornato in ''{0}''.'),
    ('es','admin.role_updated','Rol actualizado a ''{0}''.'),
    ('de','admin.role_updated','Rolle aktualisiert auf ''{0}''.')
ON DUPLICATE KEY UPDATE label_value = VALUES(label_value);

-- Fix mat.label_owner to use correct translations
UPDATE translations SET label_value = CASE language_code
    WHEN 'en' THEN 'Owner'
    WHEN 'it' THEN 'Responsabile'
    WHEN 'es' THEN 'Responsable'
    WHEN 'de' THEN 'Verantwortlicher'
END
WHERE label_key = 'mat.label_owner'
  AND label_value IN ('Author / Owner','Autore / Responsabile','Autor / Responsable','Autor / Verantwortlicher');
