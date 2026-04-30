-- 010: Admin UI + platform settings translation keys
-- Idempotent: INSERT IGNORE

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
('it','admin.manage_roles','Gestisci ruoli');

INSERT IGNORE INTO translations (language_code, label_key, label_value)
SELECT lang.code, t.label_key, t.label_value
FROM translations t
JOIN (SELECT 'es' AS code UNION ALL SELECT 'de') lang
WHERE t.language_code = 'en'
  AND t.label_key IN (
    'admin.platform_settings','admin.platform_settings_desc','admin.configure',
    'admin.doc_types','admin.doc_types_desc','admin.manage_types',
    'admin.users_roles','admin.users_roles_desc','admin.manage_users','admin.manage_roles');

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
('it','admin.delete_role_confirm','Eliminare il ruolo');

INSERT IGNORE INTO translations (language_code, label_key, label_value)
SELECT lang.code, t.label_key, t.label_value
FROM translations t
JOIN (SELECT 'es' AS code UNION ALL SELECT 'de') lang
WHERE t.language_code = 'en'
  AND t.label_key IN (
    'admin.users_tab','admin.roles_tab','admin.add_role','admin.role_name_placeholder',
    'admin.create_role','admin.role_hint','admin.role_protected','admin.edit_role',
    'admin.delete_role_blocked','admin.delete_role','admin.delete_role_confirm');

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
('it','admin.delete_area_confirm','Eliminare l''area'),
('it','admin.edit_area','Modifica area');

INSERT IGNORE INTO translations (language_code, label_key, label_value)
SELECT lang.code, t.label_key, t.label_value
FROM translations t
JOIN (SELECT 'es' AS code UNION ALL SELECT 'de') lang
WHERE t.language_code = 'en'
  AND t.label_key IN (
    'admin.areas_tab','admin.add_area','admin.area_name_placeholder','admin.create_area',
    'admin.no_areas','admin.delete_area','admin.delete_area_confirm','admin.edit_area');

INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
('en','admin.dashboard','Administration'),
('it','admin.dashboard','Amministrazione'),
('es','admin.dashboard','Administración'),
('de','admin.dashboard','Verwaltung'),
('en','admin.email_settings','Email Settings'),
('it','admin.email_settings','Impostazioni Email'),
('es','admin.email_settings','Configuración de correo'),
('de','admin.email_settings','E-Mail-Einstellungen'),
('en','admin.email_settings_desc','Configure SMTP server and send test emails'),
('it','admin.email_settings_desc','Configura il server SMTP e invia email di test'),
('es','admin.email_settings_desc','Configurar servidor SMTP y enviar correos de prueba'),
('de','admin.email_settings_desc','SMTP-Server konfigurieren und Test-E-Mails senden'),
('en','admin.configure_email','Configure email'),
('it','admin.configure_email','Configura email'),
('es','admin.configure_email','Configurar correo'),
('de','admin.configure_email','E-Mail konfigurieren'),
('en','admin.platform_settings_desc','Platform configuration and feature toggles'),
('it','admin.platform_settings_desc','Configurazione della piattaforma e abilitazione funzionalità'),
('es','admin.platform_settings_desc','Configuración de la plataforma y activación de funciones'),
('de','admin.platform_settings_desc','Plattformkonfiguration und Funktionsschalter');
