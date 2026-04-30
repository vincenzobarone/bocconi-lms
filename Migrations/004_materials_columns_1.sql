-- 004: Add columns to materials table (run once via migration tracker)

ALTER TABLE materials ADD COLUMN author_name VARCHAR(255) NULL AFTER title;
ALTER TABLE materials ADD COLUMN folder VARCHAR(255) NULL;
ALTER TABLE materials ADD COLUMN status VARCHAR(20) NOT NULL DEFAULT 'bozza';
ALTER TABLE materials ADD COLUMN protocol_number INT NULL;
ALTER TABLE materials ADD COLUMN area_id INT NULL;
ALTER TABLE materials ADD COLUMN catalogation_date DATETIME NULL;
ALTER TABLE materials ADD COLUMN page_count INT NULL;

CREATE TABLE IF NOT EXISTS material_folders (
    id         INT AUTO_INCREMENT PRIMARY KEY,
    name       VARCHAR(255) NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_name (name)
) ENGINE=InnoDB;

ALTER TABLE materials ADD COLUMN folder_id INT NULL;
ALTER TABLE materials ADD CONSTRAINT fk_material_folder FOREIGN KEY (folder_id) REFERENCES material_folders(id) ON DELETE SET NULL;
ALTER TABLE materials ADD CONSTRAINT fk_mat_area FOREIGN KEY (area_id) REFERENCES areas(id) ON DELETE SET NULL;
