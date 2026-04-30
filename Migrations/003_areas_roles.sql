-- 003: Areas, user_areas, role_permissions tables + seed default areas
-- Idempotent: CREATE TABLE IF NOT EXISTS, INSERT IGNORE

CREATE TABLE IF NOT EXISTS areas (
    id         INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    name       VARCHAR(255) NOT NULL,
    sort_order INT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS user_areas (
    user_id INT NOT NULL,
    area_id INT NOT NULL,
    PRIMARY KEY (user_id, area_id),
    CONSTRAINT fk_ua_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_ua_area FOREIGN KEY (area_id) REFERENCES areas(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT IGNORE INTO areas (name, sort_order) VALUES
('Leadership, Human Resources and Digital Technologies', 1),
('Strategy and Operations', 2),
('Finance', 3),
('Accounting', 4),
('Government, Health and not for profit', 5),
('Economics, Politics and Decision Sciences', 6),
('Law', 7),
('Marketing', 8);

CREATE TABLE IF NOT EXISTS role_permissions (
    role_id        INT NOT NULL,
    permission_key VARCHAR(50) NOT NULL,
    PRIMARY KEY (role_id, permission_key),
    CONSTRAINT fk_rp_role FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
