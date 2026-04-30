-- 009: Auto-grant menu.materials to roles that already have material permissions
-- Idempotent: INSERT IGNORE

INSERT IGNORE INTO role_permissions (role_id, permission_key)
SELECT DISTINCT role_id, 'menu.materials'
FROM role_permissions
WHERE permission_key IN ('materials.create', 'materials.edit', 'materials.approve');
