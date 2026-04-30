-- 019: Add admin.migrations_applied_badge translation key (dashboard card badge)
-- Idempotent: INSERT IGNORE

INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
('en','admin.migrations_applied_badge','applied'),
('it','admin.migrations_applied_badge','applicate'),
('es','admin.migrations_applied_badge','aplicadas'),
('de','admin.migrations_applied_badge','angewendet');
