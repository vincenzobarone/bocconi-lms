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

-- Ruoli Identity (tabelle per ASP.NET Core Identity custom stores)
CREATE TABLE IF NOT EXISTS roles (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    name            VARCHAR(256) NOT NULL,
    normalized_name VARCHAR(256) NOT NULL,
    UNIQUE KEY uk_normalized (normalized_name)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS user_roles (
    user_id         INT NOT NULL,
    role_id         INT NOT NULL,
    PRIMARY KEY (user_id, role_id),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE CASCADE
) ENGINE=InnoDB;

INSERT IGNORE INTO roles (name, normalized_name) VALUES
    ('Student', 'STUDENT'),
    ('Teacher', 'TEACHER'),
    ('Admin',   'ADMIN');

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

-- ============================================================
-- Tabella traduzioni multilingua
-- Lingue supportate: en (base), it, es, de
-- ============================================================
CREATE TABLE IF NOT EXISTS translations (
    id            INT AUTO_INCREMENT PRIMARY KEY,
    language_code VARCHAR(10)  NOT NULL,
    label_key     VARCHAR(255) NOT NULL,
    label_value   TEXT         NOT NULL,
    updated_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uk_lang_key (language_code, label_key),
    INDEX idx_language (language_code)
) ENGINE=InnoDB;

-- Seed: chiavi EN (base) + IT
INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
-- Navigation
('en', 'nav.courses',           'Courses'),
('it', 'nav.courses',           'Corsi'),
('es', 'nav.courses',           'Cursos'),
('de', 'nav.courses',           'Kurse'),
('en', 'nav.dashboard',         'Dashboard'),
('it', 'nav.dashboard',         'Dashboard'),
('es', 'nav.dashboard',         'Panel'),
('de', 'nav.dashboard',         'Dashboard'),
('en', 'nav.admin',             'Admin'),
('it', 'nav.admin',             'Admin'),
('es', 'nav.admin',             'Admin'),
('de', 'nav.admin',             'Admin'),
('en', 'nav.login',             'Sign In'),
('it', 'nav.login',             'Accedi'),
('es', 'nav.login',             'Iniciar sesión'),
('de', 'nav.login',             'Anmelden'),
('en', 'nav.logout',            'Logout'),
('it', 'nav.logout',            'Logout'),
('es', 'nav.logout',            'Cerrar sesión'),
('de', 'nav.logout',            'Abmelden'),
-- Home
('en', 'home.tagline',          'E-Learning Platform of Bocconi University'),
('it', 'home.tagline',          'Piattaforma E-Learning dell''Università Bocconi'),
('es', 'home.tagline',          'Plataforma E-Learning de la Universidad Bocconi'),
('de', 'home.tagline',          'E-Learning-Plattform der Universität Bocconi'),
('en', 'home.btn_login',        'Access the platform'),
('it', 'home.btn_login',        'Accedi alla piattaforma'),
('es', 'home.btn_login',        'Acceder a la plataforma'),
('de', 'home.btn_login',        'Zur Plattform'),
('en', 'home.btn_explore',      'Explore courses'),
('it', 'home.btn_explore',      'Esplora i corsi'),
('es', 'home.btn_explore',      'Explorar cursos'),
('de', 'home.btn_explore',      'Kurse entdecken'),
('en', 'home.featured',         'Featured courses'),
('it', 'home.featured',         'Corsi in evidenza'),
('es', 'home.featured',         'Cursos destacados'),
('de', 'home.featured',         'Empfohlene Kurse'),
('en', 'home.feature_courses',  'Structured Courses'),
('it', 'home.feature_courses',  'Corsi Strutturati'),
('es', 'home.feature_courses',  'Cursos Estructurados'),
('de', 'home.feature_courses',  'Strukturierte Kurse'),
('en', 'home.feature_courses_desc', 'Organized lessons with integrated materials and quizzes'),
('it', 'home.feature_courses_desc', 'Lezioni organizzate con materiali e quiz integrati'),
('es', 'home.feature_courses_desc', 'Lecciones organizadas con materiales y cuestionarios'),
('de', 'home.feature_courses_desc', 'Organisierte Lektionen mit Materialien und Quizzen'),
('en', 'home.feature_docs',     'Document Management'),
('it', 'home.feature_docs',     'Gestione Documenti'),
('es', 'home.feature_docs',     'Gestión de Documentos'),
('de', 'home.feature_docs',     'Dokumentenverwaltung'),
('en', 'home.feature_docs_desc','Full versioning: every upload preserves history'),
('it', 'home.feature_docs_desc','Versioning completo: ogni upload conserva la storia'),
('es', 'home.feature_docs_desc','Control de versiones: cada carga conserva el historial'),
('de', 'home.feature_docs_desc','Vollständige Versionierung: jeder Upload bewahrt die Geschichte'),
('en', 'home.feature_quiz',     'Quizzes & Progress'),
('it', 'home.feature_quiz',     'Quiz e Progressi'),
('es', 'home.feature_quiz',     'Cuestionarios y Progreso'),
('de', 'home.feature_quiz',     'Quiz & Fortschritt'),
('en', 'home.feature_quiz_desc','Online assessments with progress tracking'),
('it', 'home.feature_quiz_desc','Valutazioni online con monitoraggio dei progressi'),
('es', 'home.feature_quiz_desc','Evaluaciones en línea con seguimiento del progreso'),
('de', 'home.feature_quiz_desc','Online-Bewertungen mit Fortschrittsverfolgung'),
('en', 'home.db_error',         'Database not yet configured.'),
('it', 'home.db_error',         'Database non ancora configurato.'),
('es', 'home.db_error',         'Base de datos aún no configurada.'),
('de', 'home.db_error',         'Datenbank noch nicht konfiguriert.'),
('en', 'home.db_error_detail',  'To use the platform, run schema.sql on the MySQL server and verify the MYSQL_CONNECTION_STRING variable.'),
('it', 'home.db_error_detail',  'Per usare la piattaforma eseguire schema.sql sul server MySQL e verificare la variabile MYSQL_CONNECTION_STRING.'),
('es', 'home.db_error_detail',  'Para usar la plataforma ejecute schema.sql en el servidor MySQL y verifique la variable MYSQL_CONNECTION_STRING.'),
('de', 'home.db_error_detail',  'Um die Plattform zu nutzen, führen Sie schema.sql auf dem MySQL-Server aus und überprüfen Sie die Variable MYSQL_CONNECTION_STRING.'),
-- Auth
('en', 'auth.title',            'Sign in to the Platform'),
('it', 'auth.title',            'Accesso alla Piattaforma'),
('es', 'auth.title',            'Acceso a la Plataforma'),
('de', 'auth.title',            'Anmeldung zur Plattform'),
('en', 'auth.subtitle',         'Bocconi University — LMS'),
('it', 'auth.subtitle',         'Università Bocconi — LMS'),
('es', 'auth.subtitle',         'Universidad Bocconi — LMS'),
('de', 'auth.subtitle',         'Universität Bocconi — LMS'),
('en', 'auth.email',            'Institutional email'),
('it', 'auth.email',            'Email istituzionale'),
('es', 'auth.email',            'Correo institucional'),
('de', 'auth.email',            'Institutionelle E-Mail'),
('en', 'auth.password',         'Password'),
('it', 'auth.password',         'Password'),
('es', 'auth.password',         'Contraseña'),
('de', 'auth.password',         'Passwort'),
('en', 'auth.btn_login',        'Sign In'),
('it', 'auth.btn_login',        'Accedi'),
('es', 'auth.btn_login',        'Iniciar sesión'),
('de', 'auth.btn_login',        'Anmelden'),
-- Admin
('en', 'admin.dashboard',       'Admin Dashboard'),
('it', 'admin.dashboard',       'Dashboard Amministratore'),
('es', 'admin.dashboard',       'Panel de Administración'),
('de', 'admin.dashboard',       'Admin-Dashboard'),
('en', 'admin.new_user',        'New user'),
('it', 'admin.new_user',        'Nuovo utente'),
('es', 'admin.new_user',        'Nuevo usuario'),
('de', 'admin.new_user',        'Neuer Benutzer'),
('en', 'admin.total_courses',   'Total courses'),
('it', 'admin.total_courses',   'Corsi totali'),
('es', 'admin.total_courses',   'Cursos totales'),
('de', 'admin.total_courses',   'Kurse gesamt'),
('en', 'admin.total_users',     'Total users'),
('it', 'admin.total_users',     'Utenti totali'),
('es', 'admin.total_users',     'Usuarios totales'),
('de', 'admin.total_users',     'Benutzer gesamt'),
('en', 'admin.active_students', 'Active students'),
('it', 'admin.active_students', 'Studenti attivi'),
('es', 'admin.active_students', 'Estudiantes activos'),
('de', 'admin.active_students', 'Aktive Studenten'),
('en', 'admin.enrollments',     'Enrollments'),
('it', 'admin.enrollments',     'Iscrizioni'),
('es', 'admin.enrollments',     'Inscripciones'),
('de', 'admin.enrollments',     'Einschreibungen'),
('en', 'admin.users',           'User Management'),
('it', 'admin.users',           'Gestione Utenti'),
('es', 'admin.users',           'Gestión de Usuarios'),
('de', 'admin.users',           'Benutzerverwaltung'),
('en', 'admin.users_desc',      'Create, edit and manage user accounts'),
('it', 'admin.users_desc',      'Crea, modifica e gestisci gli account degli utenti'),
('es', 'admin.users_desc',      'Crear, editar y gestionar cuentas de usuario'),
('de', 'admin.users_desc',      'Benutzerkonten erstellen, bearbeiten und verwalten'),
('en', 'admin.manage_users',    'Manage users'),
('it', 'admin.manage_users',    'Gestisci utenti'),
('es', 'admin.manage_users',    'Gestionar usuarios'),
('de', 'admin.manage_users',    'Benutzer verwalten'),
('en', 'admin.courses',         'All Courses'),
('it', 'admin.courses',         'Tutti i Corsi'),
('es', 'admin.courses',         'Todos los Cursos'),
('de', 'admin.courses',         'Alle Kurse'),
('en', 'admin.courses_desc',    'View and manage all platform courses'),
('it', 'admin.courses_desc',    'Visualizza e gestisci tutti i corsi della piattaforma'),
('es', 'admin.courses_desc',    'Ver y gestionar todos los cursos de la plataforma'),
('de', 'admin.courses_desc',    'Alle Kurse der Plattform anzeigen und verwalten'),
('en', 'admin.view_courses',    'View courses'),
('it', 'admin.view_courses',    'Vedi corsi'),
('es', 'admin.view_courses',    'Ver cursos'),
('de', 'admin.view_courses',    'Kurse anzeigen'),
('en', 'admin.email_settings',  'Email Settings'),
('it', 'admin.email_settings',  'Impostazioni Email'),
('es', 'admin.email_settings',  'Configuración de Email'),
('de', 'admin.email_settings',  'E-Mail-Einstellungen'),
('en', 'admin.email_settings_desc', 'Configure SMTP server and send test emails'),
('it', 'admin.email_settings_desc', 'Configura il server SMTP e invia email di test'),
('es', 'admin.email_settings_desc', 'Configurar servidor SMTP y enviar correos de prueba'),
('de', 'admin.email_settings_desc', 'SMTP-Server konfigurieren und Test-E-Mails senden'),
('en', 'admin.configure_email', 'Configure email'),
('it', 'admin.configure_email', 'Configura email'),
('es', 'admin.configure_email', 'Configurar email'),
('de', 'admin.configure_email', 'E-Mail konfigurieren'),
('en', 'admin.translations',    'Translations'),
('it', 'admin.translations',    'Traduzioni'),
('es', 'admin.translations',    'Traducciones'),
('de', 'admin.translations',    'Übersetzungen'),
('en', 'admin.translations_desc','Manage interface translations for all languages'),
('it', 'admin.translations_desc','Gestisci le traduzioni dell''interfaccia per tutte le lingue'),
('es', 'admin.translations_desc','Gestionar las traducciones de la interfaz para todos los idiomas'),
('de', 'admin.translations_desc','Benutzeroberflächen-Übersetzungen für alle Sprachen verwalten'),
('en', 'admin.manage_translations','Manage translations'),
('it', 'admin.manage_translations','Gestisci traduzioni'),
('es', 'admin.manage_translations','Gestionar traducciones'),
('de', 'admin.manage_translations','Übersetzungen verwalten'),
('en', 'admin.active_teachers', 'Active teachers'),
('it', 'admin.active_teachers', 'Docenti attivi'),
('es', 'admin.active_teachers', 'Profesores activos'),
('de', 'admin.active_teachers', 'Aktive Lehrkräfte'),
('en', 'admin.total_attempts',  'Total quiz attempts'),
('it', 'admin.total_attempts',  'Tentativi quiz totali'),
('es', 'admin.total_attempts',  'Intentos de cuestionario totales'),
('de', 'admin.total_attempts',  'Gesamte Quizversuche'),
-- Footer
('en', 'footer.copyright',      'E-Learning Platform'),
('it', 'footer.copyright',      'Piattaforma E-Learning'),
('es', 'footer.copyright',      'Plataforma E-Learning'),
('de', 'footer.copyright',      'E-Learning-Plattform'),
-- Courses
('en', 'course.enrolled_count', 'enrolled'),
('it', 'course.enrolled_count', 'iscritti'),
('es', 'course.enrolled_count', 'inscritos'),
('de', 'course.enrolled_count', 'eingeschrieben'),
('en', 'course.discover',       'Discover the course'),
('it', 'course.discover',       'Scopri il corso'),
('es', 'course.discover',       'Descubrir el curso'),
('de', 'course.discover',       'Kurs entdecken');
