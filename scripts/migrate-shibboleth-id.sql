-- ============================================================
-- Migrazione: aggiunta colonna shibboleth_id a tabella users
-- Applicare su installazioni ESISTENTI (upgrade da versione
-- precedente all'integrazione Shibboleth SSO).
--
-- Idempotente: usa INFORMATION_SCHEMA per verificare se la
-- colonna esiste già prima di aggiungerla.
-- MySQL 5.7+ / 8.x compatibile.
--
-- Esecuzione (specificare il database sulla riga di comando):
--   mysql -h HOST -u USER -p NOME_DATABASE < migrate-shibboleth-id.sql
--
-- NOTA: NON includere USE qui — il database target si specifica
--       tramite l'argomento posizionale al comando mysql.
-- ============================================================

SET @col_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME   = 'users'
      AND COLUMN_NAME  = 'shibboleth_id'
);

SET @sql = IF(
    @col_exists = 0,
    'ALTER TABLE users ADD COLUMN shibboleth_id VARCHAR(255) NULL UNIQUE AFTER password_hash',
    'SELECT ''shibboleth_id già presente, nessuna modifica'' AS note'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
