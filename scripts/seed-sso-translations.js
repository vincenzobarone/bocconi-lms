#!/usr/bin/env node
/**
 * Seed script — SSO / Shibboleth translation keys (EN + IT)
 * Run from repo root:  node artifacts/bocconi-lms/scripts/seed-sso-translations.js
 *
 * Keys seeded (7 keys × 2 languages = 14 rows):
 *   sso.btn_login, sso.divider, sso.error_no_mail, sso.error_no_eppn,
 *   sso.error_not_found, sso.error_auth, auth.sso_login_success
 */

const mysql = require('mysql2/promise');

const cs  = process.env.MYSQL_CONNECTION_STRING ?? '';
const get = k => { const m = cs.match(new RegExp(k + '=([^;]+)', 'i')); return m ? m[1] : ''; };

const translations = [
  // ── SSO button + divider ────────────────────────────────────────────
  { key: 'sso.btn_login',           en: 'Sign in with Bocconi SSO',         it: 'Accedi con Bocconi SSO' },
  { key: 'sso.divider',             en: 'or continue with credentials',      it: 'oppure accedi con credenziali' },

  // ── SSO error messages ───────────────────────────────────────────────
  { key: 'sso.error_no_mail',       en: 'SSO login failed: email attribute (mail) not provided by the Identity Provider.',
                                    it: 'Accesso SSO fallito: attributo email (mail) non fornito dall\'Identity Provider.' },
  { key: 'sso.error_no_eppn',       en: 'SSO login failed: stable identifier (eduPersonPrincipalName) not provided by the Identity Provider.',
                                    it: 'Accesso SSO fallito: identificativo stabile (eduPersonPrincipalName) non fornito dall\'Identity Provider.' },
  { key: 'sso.error_not_found',     en: 'Your Bocconi account is not registered on this platform. Contact the administrator.',
                                    it: 'Il tuo account Bocconi non è registrato su questa piattaforma. Contatta l\'amministratore.' },
  { key: 'sso.error_auth',          en: 'SSO authentication error. Please try again or use local credentials.',
                                    it: 'Errore di autenticazione SSO. Riprova o usa le credenziali locali.' },

  // ── Success ──────────────────────────────────────────────────────────
  { key: 'auth.sso_login_success',  en: 'Successfully signed in via Bocconi SSO.',
                                    it: 'Accesso effettuato tramite Bocconi SSO.' },
];

(async () => {
  const conn = await mysql.createConnection({
    host:     get('server'),
    port:     parseInt(get('port') || '3306'),
    user:     get('user id') || get('uid') || get('user'),
    password: get('password') || get('pwd'),
    database: get('database'),
  });

  let inserted = 0;
  let skipped  = 0;

  for (const { key, en, it } of translations) {
    for (const [lang, val] of [['en', en], ['it', it]]) {
      const [existing] = await conn.execute(
        'SELECT id FROM translations WHERE label_key = ? AND language_code = ?',
        [key, lang]
      );
      if (existing.length > 0) {
        skipped++;
      } else {
        await conn.execute(
          'INSERT INTO translations (label_key, language_code, label_value) VALUES (?, ?, ?)',
          [key, lang, val]
        );
        inserted++;
      }
    }
  }

  console.log(`SSO translations seed: inserted=${inserted} skipped=${skipped}`);
  await conn.end();
})().catch(err => { console.error(err.message); process.exit(1); });
