-- ============================================================
-- Schema Piattaforma E-Learning Bocconi
-- MySQL — eseguire sul server MySQL prima di avviare l'app
-- ============================================================

CREATE DATABASE IF NOT EXISTS bocconi_lms CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE bocconi_lms;

-- Utenti
CREATE TABLE IF NOT EXISTS users (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    username        VARCHAR(100) NOT NULL UNIQUE,
    email           VARCHAR(255) NOT NULL UNIQUE,
    password_hash   VARCHAR(255) NOT NULL,
    first_name      VARCHAR(100) NOT NULL,
    last_name       VARCHAR(100) NOT NULL,
    role            ENUM('Student','Teacher','Admin') NOT NULL DEFAULT 'Student',
    is_active       TINYINT(1) NOT NULL DEFAULT 1,
    created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
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
    FOREIGN KEY (teacher_id) REFERENCES users(id) ON DELETE CASCADE,
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
    FOREIGN KEY (lesson_id) REFERENCES lessons(id) ON DELETE CASCADE,
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

-- ============================================================
-- Dati iniziali: utente Admin di default
-- Password: Admin@Bocconi2024 (modificare subito dopo il primo accesso)
-- Hash BCrypt generato con work factor 11
-- ============================================================
INSERT IGNORE INTO users (username, email, password_hash, first_name, last_name, role, is_active)
VALUES (
    'admin',
    'admin@bocconi.it',
    '$2a$11$WZy52/EXaD5C8z9nzCj9mujeMW8S.UV1/JCysJznILfQm2fI2sdOm',
    'Admin',
    'Bocconi',
    'Admin',
    1
);
-- NOTA: rigenerare l'hash con BCrypt prima del deploy.
-- In C#: BCrypt.Net.BCrypt.HashPassword("Admin@Bocconi2024")
