-- 023: Database admin UI + data dictionary translation keys
-- Idempotent: INSERT IGNORE

INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
-- admin.database (dashboard card title)
('en','admin.database','Database'),
('it','admin.database','Database'),
('es','admin.database','Base de datos'),
('de','admin.database','Datenbank'),

-- admin.database_page_title
('en','admin.database_page_title','Database'),
('it','admin.database_page_title','Database'),
('es','admin.database_page_title','Base de datos'),
('de','admin.database_page_title','Datenbank'),

-- admin.database_desc (dashboard card description)
('en','admin.database_desc','Generate the production installation SQL script.'),
('it','admin.database_desc','Genera lo script SQL per l''installazione in produzione.'),
('es','admin.database_desc','Genere el script SQL para la instalación en producción.'),
('de','admin.database_desc','Erzeugt das SQL-Installationsskript für die Produktion.'),

-- admin.database_manage (dashboard card button)
('en','admin.database_manage','Open'),
('it','admin.database_manage','Apri'),
('es','admin.database_manage','Abrir'),
('de','admin.database_manage','Öffnen'),

-- admin.prod_script_include_data_dictionary
('en','admin.prod_script_include_data_dictionary','Include data dictionary'),
('it','admin.prod_script_include_data_dictionary','Includi dati del dizionario'),
('es','admin.prod_script_include_data_dictionary','Incluir datos del diccionario'),
('de','admin.prod_script_include_data_dictionary','Datenwörterbuch einbeziehen'),

-- admin.prod_script_data_dictionary_hint
('en','admin.prod_script_data_dictionary_hint','If checked, inserts organisational areas, delivery platforms and translation keys (INSERT IGNORE / ON DUPLICATE KEY UPDATE).'),
('it','admin.prod_script_data_dictionary_hint','Se selezionato, inserisce aree organizzative, piattaforme di erogazione e chiavi di traduzione presenti nel DB (INSERT IGNORE / ON DUPLICATE KEY UPDATE).'),
('es','admin.prod_script_data_dictionary_hint','Si se activa, inserta áreas organizativas, plataformas de entrega y claves de traducción del DB (INSERT IGNORE / ON DUPLICATE KEY UPDATE).'),
('de','admin.prod_script_data_dictionary_hint','Wenn aktiviert, werden Organisationsbereiche, Lieferplattformen und Übersetzungsschlüssel eingefügt (INSERT IGNORE / ON DUPLICATE KEY UPDATE).');
