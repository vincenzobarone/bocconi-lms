-- 006: Convert users.role to VARCHAR(50) if it was ENUM, and fix DEFAULT
-- Safe to run regardless of current column type (MODIFY is idempotent in effect)

ALTER TABLE users MODIFY COLUMN role VARCHAR(50) NOT NULL DEFAULT '';
