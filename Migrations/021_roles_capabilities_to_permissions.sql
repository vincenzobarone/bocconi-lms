-- 021: Migrate can_teach / can_attend columns from roles to role_permissions.
--      Existing role data is migrated to permission keys courses.teach / courses.attend.
--      Columns are then dropped (idempotent via PREPARE/EXECUTE).

-- ── Migrate existing role data ─────────────────────────────────────────────────

INSERT IGNORE INTO role_permissions (role_id, permission_key)
SELECT id, 'courses.teach' FROM roles WHERE can_teach = 1;

INSERT IGNORE INTO role_permissions (role_id, permission_key)
SELECT id, 'courses.attend' FROM roles WHERE can_attend = 1;

-- ── Seed perm.courses_attend translation keys ──────────────────────────────────

INSERT INTO translations (language_code, label_key, label_value) VALUES
('en', 'perm.courses_attend', 'Attend courses as student'),
('it', 'perm.courses_attend', 'Frequenta i corsi come studente'),
('es', 'perm.courses_attend', 'Asistir a cursos como estudiante'),
('de', 'perm.courses_attend', 'Kurse als Lernender besuchen')
ON DUPLICATE KEY UPDATE label_value = VALUES(label_value);

-- ── Drop can_teach column (idempotent) ─────────────────────────────────────────

SET @col_teach = (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'roles' AND COLUMN_NAME = 'can_teach');
SET @sql = IF(@col_teach > 0, 'ALTER TABLE roles DROP COLUMN can_teach', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── Drop can_attend column (idempotent) ────────────────────────────────────────

SET @col_attend = (SELECT COUNT(*) FROM information_schema.COLUMNS
                   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'roles' AND COLUMN_NAME = 'can_attend');
SET @sql = IF(@col_attend > 0, 'ALTER TABLE roles DROP COLUMN can_attend', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
