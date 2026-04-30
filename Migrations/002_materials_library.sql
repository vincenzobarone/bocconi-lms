-- 002: Materials Library tables + document types seed
-- Idempotent: CREATE TABLE IF NOT EXISTS + INSERT IGNORE

CREATE TABLE IF NOT EXISTS document_types (
    id         INT AUTO_INCREMENT PRIMARY KEY,
    name       VARCHAR(255) NOT NULL UNIQUE,
    sort_order INT NOT NULL DEFAULT 0
) ENGINE=InnoDB;

INSERT IGNORE INTO document_types (name, sort_order) VALUES
('Allegati',1),('Articolo non pubblicato',2),('Atti del Convegno',3),
('Caso',4),('Esercitazione',5),('Incident',6),('Manuale',7),
('Materiale audiovisivo',8),('Norme e Leggi',9),('Nota',10),
('Paper',11),('Questionario',12),('Report di Ricerca',13),
('Role Playing - Simulazione',14),('Scheda - Griglia',15),
('SDA Case Collection / ECCH',16),
('SDA Case Collection Background Note / ECCH',17),
('SDA Case Collection Instructor Spreadsheet / ECCH',18),
('SDA Case Collection Role Playing / ECCH',19),
('SDA Case Collection Slide / ECCH',20),
('SDA Case Collection Supplementary software / ECCH',21),
('SDA Case Collection Teaching Notes / ECCH',22),
('SDA Case Collection Teaching Notes Supplement software / ECCH',23),
('SDA Case Collection Instructor presentation material / ECCH',24),
('Slides',25),('Soluzione caso',26),('Teaching Notes',27),
('Traduzione autorizzata articoli e capitoli',28),
('Traduzione autorizzata caso',29);

CREATE TABLE IF NOT EXISTS materials (
    id               INT AUTO_INCREMENT PRIMARY KEY,
    title            VARCHAR(255) NOT NULL,
    owner_id         INT NULL,
    language         VARCHAR(50) NOT NULL DEFAULT 'Italiano',
    document_type_id INT NULL,
    created_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_title (title),
    FOREIGN KEY (owner_id)         REFERENCES users(id)          ON DELETE SET NULL,
    FOREIGN KEY (document_type_id) REFERENCES document_types(id) ON DELETE SET NULL,
    INDEX idx_owner (owner_id),
    INDEX idx_type  (document_type_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS material_versions (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    material_id     INT NOT NULL,
    version_number  INT NOT NULL,
    file_name       VARCHAR(255) NOT NULL,
    file_path       VARCHAR(500) NOT NULL,
    file_type       VARCHAR(20)  NOT NULL,
    file_size_bytes BIGINT NOT NULL DEFAULT 0,
    uploaded_by     INT NOT NULL,
    notes           TEXT,
    is_active       TINYINT(1) NOT NULL DEFAULT 1,
    uploaded_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (material_id) REFERENCES materials(id) ON DELETE CASCADE,
    FOREIGN KEY (uploaded_by) REFERENCES users(id),
    UNIQUE KEY uniq_ver (material_id, version_number),
    INDEX idx_material (material_id),
    INDEX idx_active   (material_id, is_active)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS lesson_materials (
    lesson_id   INT NOT NULL,
    material_id INT NOT NULL,
    added_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    added_by    INT NULL,
    PRIMARY KEY (lesson_id, material_id),
    FOREIGN KEY (lesson_id)   REFERENCES lessons(id)   ON DELETE CASCADE,
    FOREIGN KEY (material_id) REFERENCES materials(id) ON DELETE CASCADE,
    FOREIGN KEY (added_by)    REFERENCES users(id)     ON DELETE SET NULL
) ENGINE=InnoDB;
