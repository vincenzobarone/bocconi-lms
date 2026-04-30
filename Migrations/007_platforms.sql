-- 007: Create platforms table + add publish columns to materials (run once via tracker)

CREATE TABLE IF NOT EXISTS platforms (
    id         INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    name       VARCHAR(255) NOT NULL,
    sort_order INT NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_platform_name (name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

ALTER TABLE materials ADD COLUMN is_publishable TINYINT(1) NOT NULL DEFAULT 0;
ALTER TABLE materials ADD COLUMN external_protocol_code VARCHAR(100) NULL;
ALTER TABLE materials ADD COLUMN platform_id INT NULL;
ALTER TABLE materials ADD CONSTRAINT fk_mat_platform FOREIGN KEY (platform_id) REFERENCES platforms(id) ON DELETE SET NULL;
ALTER TABLE materials ADD COLUMN is_published TINYINT(1) NOT NULL DEFAULT 0;
ALTER TABLE materials ADD COLUMN external_link VARCHAR(500) NULL;
