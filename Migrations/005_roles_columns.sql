-- 005: Add can_teach + can_attend columns to roles; fix users.role DEFAULT (run once)

ALTER TABLE roles ADD COLUMN can_teach TINYINT(1) NOT NULL DEFAULT 0;
ALTER TABLE roles ADD COLUMN can_attend TINYINT(1) NOT NULL DEFAULT 0;
ALTER TABLE users MODIFY COLUMN role VARCHAR(50) NOT NULL DEFAULT '';
