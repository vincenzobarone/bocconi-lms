-- 000: Full baseline schema — creates all tables in their final current state.
-- Safe on both fresh and existing DBs (CREATE TABLE IF NOT EXISTS throughout).
-- This migration bootstraps a completely empty database.

-- ── Core identity tables ───────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS users (
    id            INT AUTO_INCREMENT PRIMARY KEY,
    email         VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    first_name    VARCHAR(100) NOT NULL,
    last_name     VARCHAR(100) NOT NULL,
    role          VARCHAR(50)  NOT NULL DEFAULT '',
    is_active     TINYINT(1)   NOT NULL DEFAULT 1,
    created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by    INT NULL,
    INDEX idx_email (email),
    INDEX idx_role  (role)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS roles (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    name            VARCHAR(256) NOT NULL,
    normalized_name VARCHAR(256) NOT NULL,
    can_teach       TINYINT(1)   NOT NULL DEFAULT 0,
    can_attend      TINYINT(1)   NOT NULL DEFAULT 0,
    created_at      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by      INT NULL,
    UNIQUE KEY uk_normalized (normalized_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Courses module ────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS courses (
    id           INT AUTO_INCREMENT PRIMARY KEY,
    title        VARCHAR(255) NOT NULL,
    description  TEXT         NOT NULL,
    category     VARCHAR(100) NOT NULL,
    teacher_id   INT          NOT NULL,
    start_date   DATE,
    end_date     DATE,
    is_published TINYINT(1)   NOT NULL DEFAULT 0,
    created_at   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by   INT NULL,
    FOREIGN KEY (teacher_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL,
    INDEX idx_teacher   (teacher_id),
    INDEX idx_published (is_published)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS lessons (
    id           INT AUTO_INCREMENT PRIMARY KEY,
    course_id    INT         NOT NULL,
    title        VARCHAR(255) NOT NULL,
    content      TEXT,
    sort_order   INT         NOT NULL DEFAULT 0,
    is_published TINYINT(1)  NOT NULL DEFAULT 0,
    created_at   DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE CASCADE,
    INDEX idx_course (course_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- documents and document_versions: kept for legacy data; dropped by 008_drop_legacy.sql
CREATE TABLE IF NOT EXISTS documents (
    id         INT AUTO_INCREMENT PRIMARY KEY,
    lesson_id  INT          NOT NULL,
    title      VARCHAR(255) NOT NULL,
    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (lesson_id) REFERENCES lessons(id) ON DELETE CASCADE,
    INDEX idx_lesson (lesson_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS document_versions (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    document_id     INT          NOT NULL,
    version_number  INT          NOT NULL,
    file_name       VARCHAR(255) NOT NULL,
    file_path       VARCHAR(500) NOT NULL,
    file_type       VARCHAR(20)  NOT NULL,
    file_size_bytes BIGINT       NOT NULL DEFAULT 0,
    uploaded_by     INT          NOT NULL,
    notes           TEXT,
    is_active       TINYINT(1)   NOT NULL DEFAULT 1,
    uploaded_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    FOREIGN KEY (uploaded_by) REFERENCES users(id),
    UNIQUE KEY uniq_version (document_id, version_number),
    INDEX idx_document (document_id),
    INDEX idx_active   (document_id, is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS quizzes (
    id                 INT AUTO_INCREMENT PRIMARY KEY,
    lesson_id          INT          NOT NULL,
    title              VARCHAR(255) NOT NULL,
    description        TEXT,
    time_limit_minutes INT          NOT NULL DEFAULT 30,
    passing_score      INT          NOT NULL DEFAULT 60,
    created_at         DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by         INT NULL,
    FOREIGN KEY (lesson_id)  REFERENCES lessons(id) ON DELETE CASCADE,
    FOREIGN KEY (created_by) REFERENCES users(id)   ON DELETE SET NULL,
    INDEX idx_lesson (lesson_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS quiz_questions (
    id            INT AUTO_INCREMENT PRIMARY KEY,
    quiz_id       INT  NOT NULL,
    question_text TEXT NOT NULL,
    sort_order    INT  NOT NULL DEFAULT 0,
    FOREIGN KEY (quiz_id) REFERENCES quizzes(id) ON DELETE CASCADE,
    INDEX idx_quiz (quiz_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS quiz_options (
    id          INT AUTO_INCREMENT PRIMARY KEY,
    question_id INT         NOT NULL,
    option_text TEXT        NOT NULL,
    is_correct  TINYINT(1)  NOT NULL DEFAULT 0,
    sort_order  INT         NOT NULL DEFAULT 0,
    FOREIGN KEY (question_id) REFERENCES quiz_questions(id) ON DELETE CASCADE,
    INDEX idx_question (question_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS enrollments (
    id          INT AUTO_INCREMENT PRIMARY KEY,
    user_id     INT      NOT NULL,
    course_id   INT      NOT NULL,
    enrolled_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uniq_enrollment (user_id, course_id),
    FOREIGN KEY (user_id)   REFERENCES users(id)   ON DELETE CASCADE,
    FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE CASCADE,
    INDEX idx_user   (user_id),
    INDEX idx_course (course_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS lesson_progress (
    id           INT AUTO_INCREMENT PRIMARY KEY,
    user_id      INT      NOT NULL,
    lesson_id    INT      NOT NULL,
    completed_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uniq_progress (user_id, lesson_id),
    FOREIGN KEY (user_id)   REFERENCES users(id)   ON DELETE CASCADE,
    FOREIGN KEY (lesson_id) REFERENCES lessons(id) ON DELETE CASCADE,
    INDEX idx_user   (user_id),
    INDEX idx_lesson (lesson_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS quiz_attempts (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    quiz_id         INT      NOT NULL,
    user_id         INT      NOT NULL,
    score           INT      NOT NULL,
    total_questions INT      NOT NULL,
    correct_answers INT      NOT NULL,
    passed          TINYINT(1) NOT NULL DEFAULT 0,
    attempted_at    DATETIME   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (quiz_id) REFERENCES quizzes(id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(id)   ON DELETE CASCADE,
    INDEX idx_quiz (quiz_id),
    INDEX idx_user (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Auth ──────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS password_reset_tokens (
    id         INT         AUTO_INCREMENT PRIMARY KEY,
    user_id    INT         NOT NULL,
    token      VARCHAR(64) NOT NULL,
    expires_at DATETIME    NOT NULL,
    used       TINYINT(1)  NOT NULL DEFAULT 0,
    created_at DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_token (token),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_token (token),
    INDEX idx_user  (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Areas ─────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS areas (
    id         INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
    name       VARCHAR(255) NOT NULL,
    sort_order INT          NOT NULL DEFAULT 0,
    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by INT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS user_areas (
    user_id INT NOT NULL,
    area_id INT NOT NULL,
    PRIMARY KEY (user_id, area_id),
    CONSTRAINT fk_ua_user FOREIGN KEY (user_id) REFERENCES users(id)  ON DELETE CASCADE,
    CONSTRAINT fk_ua_area FOREIGN KEY (area_id) REFERENCES areas(id)  ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Translations ───────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS translations (
    id            INT          AUTO_INCREMENT PRIMARY KEY,
    language_code VARCHAR(10)  NOT NULL,
    label_key     VARCHAR(255) NOT NULL,
    label_value   TEXT         NOT NULL,
    created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uk_lang_key (language_code, label_key),
    INDEX idx_language (language_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Materials library ──────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS document_types (
    id         INT          AUTO_INCREMENT PRIMARY KEY,
    name       VARCHAR(255) NOT NULL UNIQUE,
    sort_order INT          NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS material_folders (
    id         INT          AUTO_INCREMENT PRIMARY KEY,
    name       VARCHAR(255) NOT NULL,
    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_name (name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Platforms created before materials to allow FK reference
CREATE TABLE IF NOT EXISTS platforms (
    id         INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
    name       VARCHAR(255) NOT NULL,
    sort_order INT          NOT NULL DEFAULT 0,
    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_platform_name (name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Full final definition including all columns and FK constraints
CREATE TABLE IF NOT EXISTS materials (
    id                     INT          AUTO_INCREMENT PRIMARY KEY,
    title                  VARCHAR(255) NOT NULL,
    author_name            VARCHAR(255) NULL,
    owner_id               INT          NULL,
    language               VARCHAR(50)  NOT NULL DEFAULT 'Italiano',
    document_type_id       INT          NULL,
    status                 VARCHAR(50)  NOT NULL DEFAULT 'bozza',
    protocol_number        INT          NULL,
    folder_id              INT          NULL,
    folder                 VARCHAR(255) NULL,
    area_id                INT          NULL,
    catalogation_date      DATE         NULL,
    page_count             INT          NULL,
    is_publishable         TINYINT(1)   NOT NULL DEFAULT 0,
    external_protocol_code VARCHAR(100) NULL,
    platform_id            INT          NULL,
    is_published           TINYINT(1)   NOT NULL DEFAULT 0,
    external_link          VARCHAR(500) NULL,
    created_at             DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_title (title),
    CONSTRAINT fk_mat_owner    FOREIGN KEY (owner_id)         REFERENCES users(id)           ON DELETE SET NULL,
    CONSTRAINT fk_mat_doc_type FOREIGN KEY (document_type_id) REFERENCES document_types(id)  ON DELETE SET NULL,
    CONSTRAINT fk_material_folder FOREIGN KEY (folder_id)     REFERENCES material_folders(id) ON DELETE SET NULL,
    CONSTRAINT fk_mat_area     FOREIGN KEY (area_id)          REFERENCES areas(id)            ON DELETE SET NULL,
    CONSTRAINT fk_mat_platform FOREIGN KEY (platform_id)      REFERENCES platforms(id)        ON DELETE SET NULL,
    INDEX idx_owner  (owner_id),
    INDEX idx_type   (document_type_id),
    INDEX idx_status (status),
    INDEX idx_folder (folder_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS material_versions (
    id              INT          AUTO_INCREMENT PRIMARY KEY,
    material_id     INT          NOT NULL,
    version_number  INT          NOT NULL,
    file_name       VARCHAR(255) NOT NULL,
    file_path       VARCHAR(500) NOT NULL,
    file_type       VARCHAR(20)  NOT NULL,
    file_size_bytes BIGINT       NOT NULL DEFAULT 0,
    uploaded_by     INT          NOT NULL,
    notes           TEXT,
    is_active       TINYINT(1)   NOT NULL DEFAULT 1,
    uploaded_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (material_id) REFERENCES materials(id) ON DELETE CASCADE,
    FOREIGN KEY (uploaded_by) REFERENCES users(id),
    UNIQUE KEY uniq_ver (material_id, version_number),
    INDEX idx_material (material_id),
    INDEX idx_active   (material_id, is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS lesson_materials (
    lesson_id   INT      NOT NULL,
    material_id INT      NOT NULL,
    added_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    added_by    INT      NULL,
    PRIMARY KEY (lesson_id, material_id),
    FOREIGN KEY (lesson_id)   REFERENCES lessons(id)   ON DELETE CASCADE,
    FOREIGN KEY (material_id) REFERENCES materials(id) ON DELETE CASCADE,
    FOREIGN KEY (added_by)    REFERENCES users(id)     ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Permissions ────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS role_permissions (
    role_id        INT         NOT NULL,
    permission_key VARCHAR(50) NOT NULL,
    PRIMARY KEY (role_id, permission_key),
    CONSTRAINT fk_rp_role FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
