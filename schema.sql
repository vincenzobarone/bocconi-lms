-- ============================================================
-- Schema Piattaforma E-Learning Bocconi
-- MySQL — eseguire sul server MySQL prima di avviare l'app
-- ============================================================

CREATE DATABASE IF NOT EXISTS bocconi_lms CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE bocconi_lms;

-- Utenti
CREATE TABLE IF NOT EXISTS users (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    email           VARCHAR(255) NOT NULL UNIQUE,
    password_hash   VARCHAR(255) NOT NULL,
    shibboleth_id   VARCHAR(255) NULL UNIQUE,
    first_name      VARCHAR(100) NOT NULL,
    last_name       VARCHAR(100) NOT NULL,
    role            VARCHAR(256) NOT NULL DEFAULT 'Student',
    is_active       TINYINT(1) NOT NULL DEFAULT 1,
    created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by      INT NULL,
    INDEX idx_email (email),
    INDEX idx_role  (role)
) ENGINE=InnoDB;

-- Corsi
CREATE TABLE IF NOT EXISTS courses (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    title           VARCHAR(255) NOT NULL,
    description     TEXT NOT NULL,
    category        VARCHAR(100) NOT NULL,
    teacher_id      INT NOT NULL,
    start_date      DATE,
    end_date        DATE,
    is_published    TINYINT(1) NOT NULL DEFAULT 0,
    created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by      INT NULL,
    FOREIGN KEY (teacher_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL,
    INDEX idx_teacher (teacher_id),
    INDEX idx_published (is_published)
) ENGINE=InnoDB;

-- Lezioni
CREATE TABLE IF NOT EXISTS lessons (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    course_id       INT NOT NULL,
    title           VARCHAR(255) NOT NULL,
    content         TEXT,
    sort_order      INT NOT NULL DEFAULT 0,
    is_published    TINYINT(1) NOT NULL DEFAULT 0,
    created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE CASCADE,
    INDEX idx_course (course_id)
) ENGINE=InnoDB;

-- Documenti
CREATE TABLE IF NOT EXISTS documents (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    lesson_id       INT NOT NULL,
    title           VARCHAR(255) NOT NULL,
    created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (lesson_id) REFERENCES lessons(id) ON DELETE CASCADE,
    INDEX idx_lesson (lesson_id)
) ENGINE=InnoDB;

-- Versioni documenti (versioning)
CREATE TABLE IF NOT EXISTS document_versions (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    document_id     INT NOT NULL,
    version_number  INT NOT NULL,
    file_name       VARCHAR(255) NOT NULL,
    file_path       VARCHAR(500) NOT NULL,
    file_type       VARCHAR(20) NOT NULL,
    file_size_bytes BIGINT NOT NULL DEFAULT 0,
    uploaded_by     INT NOT NULL,
    notes           TEXT,
    is_active       TINYINT(1) NOT NULL DEFAULT 1,
    uploaded_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    FOREIGN KEY (uploaded_by) REFERENCES users(id),
    UNIQUE KEY uniq_version (document_id, version_number),
    INDEX idx_document  (document_id),
    INDEX idx_active    (document_id, is_active)
) ENGINE=InnoDB;

-- Quiz
CREATE TABLE IF NOT EXISTS quizzes (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    lesson_id           INT NOT NULL,
    title               VARCHAR(255) NOT NULL,
    description         TEXT,
    time_limit_minutes  INT NOT NULL DEFAULT 30,
    passing_score       INT NOT NULL DEFAULT 60,
    created_at          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by          INT NULL,
    FOREIGN KEY (lesson_id) REFERENCES lessons(id) ON DELETE CASCADE,
    FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL,
    INDEX idx_lesson (lesson_id)
) ENGINE=InnoDB;

-- Domande quiz
CREATE TABLE IF NOT EXISTS quiz_questions (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    quiz_id         INT NOT NULL,
    question_text   TEXT NOT NULL,
    sort_order      INT NOT NULL DEFAULT 0,
    FOREIGN KEY (quiz_id) REFERENCES quizzes(id) ON DELETE CASCADE,
    INDEX idx_quiz (quiz_id)
) ENGINE=InnoDB;

-- Opzioni risposte
CREATE TABLE IF NOT EXISTS quiz_options (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    question_id     INT NOT NULL,
    option_text     TEXT NOT NULL,
    is_correct      TINYINT(1) NOT NULL DEFAULT 0,
    sort_order      INT NOT NULL DEFAULT 0,
    FOREIGN KEY (question_id) REFERENCES quiz_questions(id) ON DELETE CASCADE,
    INDEX idx_question (question_id)
) ENGINE=InnoDB;

-- Iscrizioni ai corsi
CREATE TABLE IF NOT EXISTS enrollments (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    user_id         INT NOT NULL,
    course_id       INT NOT NULL,
    enrolled_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uniq_enrollment (user_id, course_id),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE CASCADE,
    INDEX idx_user   (user_id),
    INDEX idx_course (course_id)
) ENGINE=InnoDB;

-- Progressi lezioni
CREATE TABLE IF NOT EXISTS lesson_progress (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    user_id         INT NOT NULL,
    lesson_id       INT NOT NULL,
    completed_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uniq_progress (user_id, lesson_id),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (lesson_id) REFERENCES lessons(id) ON DELETE CASCADE,
    INDEX idx_user   (user_id),
    INDEX idx_lesson (lesson_id)
) ENGINE=InnoDB;

-- Tentativi quiz
CREATE TABLE IF NOT EXISTS quiz_attempts (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    quiz_id             INT NOT NULL,
    user_id             INT NOT NULL,
    score               INT NOT NULL,
    total_questions     INT NOT NULL,
    correct_answers     INT NOT NULL,
    passed              TINYINT(1) NOT NULL DEFAULT 0,
    attempted_at        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (quiz_id) REFERENCES quizzes(id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_quiz (quiz_id),
    INDEX idx_user (user_id)
) ENGINE=InnoDB;

-- Token di reset password
CREATE TABLE IF NOT EXISTS password_reset_tokens (
    id          INT AUTO_INCREMENT PRIMARY KEY,
    user_id     INT NOT NULL,
    token       VARCHAR(64) NOT NULL,
    expires_at  DATETIME NOT NULL,
    used        TINYINT(1) NOT NULL DEFAULT 0,
    created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_token (token),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_token (token),
    INDEX idx_user  (user_id)
) ENGINE=InnoDB;

-- Ruoli Identity (tabelle per ASP.NET Core Identity custom stores)
CREATE TABLE IF NOT EXISTS roles (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    name            VARCHAR(256) NOT NULL,
    normalized_name VARCHAR(256) NOT NULL,
    created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by      INT NULL,
    UNIQUE KEY uk_normalized (normalized_name)
) ENGINE=InnoDB;

INSERT IGNORE INTO roles (name, normalized_name) VALUES
    ('Admin',   'ADMIN');

-- Aree didattiche
CREATE TABLE IF NOT EXISTS areas (
    id         INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    name       VARCHAR(255) NOT NULL,
    sort_order INT NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by INT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS user_areas (
    user_id INT NOT NULL,
    area_id INT NOT NULL,
    PRIMARY KEY (user_id, area_id),
    CONSTRAINT fk_ua_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_ua_area FOREIGN KEY (area_id) REFERENCES areas(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- Dati iniziali: utente Admin di default
-- Password: Admin@Bocconi2024 (modificare subito dopo il primo accesso)
-- Hash BCrypt generato con work factor 11
-- ============================================================
INSERT IGNORE INTO users (email, password_hash, first_name, last_name, role, is_active)
VALUES (
    'admin@bocconi.it',
    '$2a$11$WZy52/EXaD5C8z9nzCj9mujeMW8S.UV1/JCysJznILfQm2fI2sdOm',
    'Admin',
    'Bocconi',
    'Admin',
    1
);
-- NOTA: rigenerare l'hash con BCrypt prima del deploy.
-- In C#: BCrypt.Net.BCrypt.HashPassword("Admin@Bocconi2024")

-- ============================================================
-- Libreria Materiali didattici
-- ============================================================

-- Tipi documento (elenco modificabile da Admin)
CREATE TABLE IF NOT EXISTS document_types (
    id          INT AUTO_INCREMENT PRIMARY KEY,
    name        VARCHAR(255) NOT NULL UNIQUE,
    sort_order  INT NOT NULL DEFAULT 0
) ENGINE=InnoDB;

INSERT IGNORE INTO document_types (name, sort_order) VALUES
('Allegati', 1),
('Articolo non pubblicato', 2),
('Atti del Convegno', 3),
('Caso', 4),
('Esercitazione', 5),
('Incident', 6),
('Manuale', 7),
('Materiale audiovisivo', 8),
('Norme e Leggi', 9),
('Nota', 10),
('Paper', 11),
('Questionario', 12),
('Report di Ricerca', 13),
('Role Playing - Simulazione', 14),
('Scheda - Griglia', 15),
('SDA Case Collection / ECCH', 16),
('SDA Case Collection Background Note / ECCH', 17),
('SDA Case Collection Instructor Spreadsheet / ECCH', 18),
('SDA Case Collection Role Playing / ECCH', 19),
('SDA Case Collection Slide / ECCH', 20),
('SDA Case Collection Supplementary software / ECCH', 21),
('SDA Case Collection Teaching Notes / ECCH', 22),
('SDA Case Collection Teaching Notes Supplement software / ECCH', 23),
('SDA Case Collection Instructor presentation material / ECCH', 24),
('Slides', 25),
('Soluzione caso', 26),
('Teaching Notes', 27),
('Traduzione autorizzata articoli e capitoli', 28),
('Traduzione autorizzata caso', 29);

-- Cartelle per la libreria materiali
CREATE TABLE IF NOT EXISTS material_folders (
    id         INT AUTO_INCREMENT PRIMARY KEY,
    name       VARCHAR(255) NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_name (name)
) ENGINE=InnoDB;

-- ============================================================
-- Autori (Task #49 - autori multipli)
-- Sostituisce la colonna denormalizzata materials.author_name
-- ============================================================
CREATE TABLE IF NOT EXISTS authors (
    id          INT AUTO_INCREMENT PRIMARY KEY,
    full_name   VARCHAR(255) NOT NULL,
    email       VARCHAR(255) NULL,
    affiliation VARCHAR(255) NULL,
    created_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_full_name (full_name),
    INDEX idx_email (email)
) ENGINE=InnoDB;

-- Libreria materiali (repository centrale)
-- NOTA: author_name rimosso in Task #49 — usare material_authors per la relazione N:N
CREATE TABLE IF NOT EXISTS materials (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    title               VARCHAR(255) NOT NULL,
    owner_id            INT NULL,
    language            VARCHAR(50) NOT NULL DEFAULT 'Italiano',
    document_type_id    INT NULL,
    status              VARCHAR(50) NOT NULL DEFAULT 'draft',
    protocol_number     INT NULL,
    folder_id           INT NULL,
    area_id             INT NULL,
    catalogation_date   DATE NULL,
    created_at          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_title (title),
    FOREIGN KEY (owner_id) REFERENCES users(id) ON DELETE SET NULL,
    FOREIGN KEY (document_type_id) REFERENCES document_types(id) ON DELETE SET NULL,
    FOREIGN KEY (folder_id) REFERENCES material_folders(id) ON DELETE SET NULL,
    INDEX idx_owner  (owner_id),
    INDEX idx_type   (document_type_id),
    INDEX idx_status (status),
    INDEX idx_folder (folder_id)
) ENGINE=InnoDB;

-- Relazione N:N materiali ↔ autori (con ordinamento esplicito)
CREATE TABLE IF NOT EXISTS material_authors (
    material_id INT NOT NULL,
    author_id   INT NOT NULL,
    sort_order  INT NOT NULL DEFAULT 0,
    PRIMARY KEY (material_id, author_id),
    FOREIGN KEY (material_id) REFERENCES materials(id) ON DELETE CASCADE,
    FOREIGN KEY (author_id)   REFERENCES authors(id)   ON DELETE CASCADE,
    INDEX idx_author   (author_id),
    INDEX idx_material (material_id)
) ENGINE=InnoDB;

-- Versioni file dei materiali
CREATE TABLE IF NOT EXISTS material_versions (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    material_id     INT NOT NULL,
    version_number  INT NOT NULL,
    file_name       VARCHAR(255) NOT NULL,
    file_path       VARCHAR(500) NOT NULL,
    file_type       VARCHAR(20) NOT NULL,
    file_size_bytes BIGINT NOT NULL DEFAULT 0,
    uploaded_by     INT NOT NULL,
    notes           TEXT,
    is_active       TINYINT(1) NOT NULL DEFAULT 1,
    uploaded_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (material_id) REFERENCES materials(id) ON DELETE CASCADE,
    FOREIGN KEY (uploaded_by) REFERENCES users(id),
    UNIQUE KEY uniq_version (material_id, version_number),
    INDEX idx_material (material_id),
    INDEX idx_active   (material_id, is_active)
) ENGINE=InnoDB;

-- Collegamento materiali → lezioni
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

-- ============================================================
-- Log di sistema (HTTP access + audit events)
-- ============================================================
CREATE TABLE IF NOT EXISTS system_logs (
    id          BIGINT AUTO_INCREMENT PRIMARY KEY,
    log_type    VARCHAR(20)  NOT NULL,
    user_email  VARCHAR(255) NULL,
    ip          VARCHAR(45)  NULL,
    action      VARCHAR(500) NOT NULL,
    target      VARCHAR(500) NULL,
    outcome     VARCHAR(50)  NULL,
    duration_ms INT          NULL,
    created_at  DATETIME(3)  NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    INDEX idx_created (created_at),
    INDEX idx_user    (user_email),
    INDEX idx_type    (log_type)
) ENGINE=InnoDB;

-- ============================================================
-- API Keys per accesso esterno (es. lettura log via /api/v1/logs)
-- prefix in chiaro per lookup; secret salvato come BCrypt hash.
-- ============================================================
CREATE TABLE IF NOT EXISTS api_keys (
    id            INT AUTO_INCREMENT PRIMARY KEY,
    name          VARCHAR(100) NOT NULL,
    key_prefix    VARCHAR(16)  NOT NULL,
    key_hash      VARCHAR(255) NOT NULL,
    scopes        VARCHAR(255) NOT NULL DEFAULT 'logs:read',
    created_by    VARCHAR(255) NULL,
    created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_used_at  DATETIME     NULL,
    revoked_at    DATETIME     NULL,
    UNIQUE KEY uk_api_keys_prefix (key_prefix),
    INDEX idx_api_keys_revoked (revoked_at)
) ENGINE=InnoDB;

-- ============================================================
-- Tabella traduzioni multilingua
-- Lingue supportate: en (base), it, es, de
-- ============================================================
-- ============================================================
-- MIGRATION: installazioni esistenti (non necessaria su fresh install)
-- Eseguire UNA SOLA VOLTA sugli ambienti già in produzione:
--
--   -- 1. Crea tabelle se non esistono
--   CREATE TABLE IF NOT EXISTS authors (...);     -- vedi sopra
--   CREATE TABLE IF NOT EXISTS material_authors (...); -- vedi sopra
--
--   -- 2. Migra i dati legacy (author_name → authors + material_authors)
--   INSERT IGNORE INTO authors (full_name)
--     SELECT DISTINCT TRIM(author_name)
--     FROM materials
--     WHERE author_name IS NOT NULL AND TRIM(author_name) <> '';
--
--   INSERT IGNORE INTO material_authors (material_id, author_id, sort_order)
--     SELECT m.id, a.id, 0
--     FROM materials m
--     JOIN authors a ON a.full_name = TRIM(m.author_name)
--     WHERE m.author_name IS NOT NULL AND TRIM(m.author_name) <> '';
--
--   -- 3. Rimuovi colonna legacy (solo dopo verifica)
--   ALTER TABLE materials DROP COLUMN author_name;
-- ============================================================

CREATE TABLE IF NOT EXISTS translations (
    id            INT AUTO_INCREMENT PRIMARY KEY,
    language_code VARCHAR(10)  NOT NULL,
    label_key     VARCHAR(255) NOT NULL,
    label_value   TEXT         NOT NULL,
    created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uk_lang_key (language_code, label_key),
    INDEX idx_language (language_code)
) ENGINE=InnoDB;
