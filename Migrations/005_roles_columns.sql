-- 005: Fix users.role DEFAULT (idempotent)
-- Note: can_teach / can_attend columns were originally added here but were
-- subsequently removed by migration 021 (moved to role_permissions as
-- permission keys courses.teach / courses.attend).

ALTER TABLE users MODIFY COLUMN role VARCHAR(50) NOT NULL DEFAULT '';
