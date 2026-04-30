-- 007: Create platforms table + add publish columns to materials (idempotent)
-- Uses PREPARE/EXECUTE to skip columns/constraints that already exist.

CREATE TABLE IF NOT EXISTS platforms (
    id         INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
    name       VARCHAR(255) NOT NULL,
    sort_order INT          NOT NULL DEFAULT 0,
    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_platform_name (name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

SET @sql = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='materials' AND COLUMN_NAME='is_publishable')=0,'ALTER TABLE materials ADD COLUMN is_publishable TINYINT(1) NOT NULL DEFAULT 0','SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='materials' AND COLUMN_NAME='external_protocol_code')=0,'ALTER TABLE materials ADD COLUMN external_protocol_code VARCHAR(100) NULL','SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='materials' AND COLUMN_NAME='platform_id')=0,'ALTER TABLE materials ADD COLUMN platform_id INT NULL','SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF((SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='materials' AND CONSTRAINT_NAME='fk_mat_platform' AND CONSTRAINT_TYPE='FOREIGN KEY')=0,'ALTER TABLE materials ADD CONSTRAINT fk_mat_platform FOREIGN KEY (platform_id) REFERENCES platforms(id) ON DELETE SET NULL','SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='materials' AND COLUMN_NAME='is_published')=0,'ALTER TABLE materials ADD COLUMN is_published TINYINT(1) NOT NULL DEFAULT 0','SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='materials' AND COLUMN_NAME='external_link')=0,'ALTER TABLE materials ADD COLUMN external_link VARCHAR(500) NULL','SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
