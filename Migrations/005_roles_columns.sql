-- 005: Add can_teach + can_attend columns to roles; fix users.role DEFAULT (idempotent)
-- Uses PREPARE/EXECUTE to skip columns that already exist.

SET @sql = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='roles' AND COLUMN_NAME='can_teach')=0,'ALTER TABLE roles ADD COLUMN can_teach TINYINT(1) NOT NULL DEFAULT 0','SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='roles' AND COLUMN_NAME='can_attend')=0,'ALTER TABLE roles ADD COLUMN can_attend TINYINT(1) NOT NULL DEFAULT 0','SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

ALTER TABLE users MODIFY COLUMN role VARCHAR(50) NOT NULL DEFAULT '';
