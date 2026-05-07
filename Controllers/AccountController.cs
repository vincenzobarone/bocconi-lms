using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using BocconiLMS.Data;
using BocconiLMS.Models;
using BocconiLMS.Services;

namespace BocconiLMS.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly DbHelper _db;
    private readonly EmailService _emailService;
    private readonly ILogger<AccountController> _logger;
    private readonly IConfiguration _config;
    private readonly IAuditLogger _audit;
    private readonly UserRepository _userRepository;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        DbHelper db,
        EmailService emailService,
        ILogger<AccountController> logger,
        IConfiguration config,
        IAuditLogger audit,
        UserRepository userRepository)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _emailService = emailService;
        _logger = logger;
        _config = config;
        _audit = audit;
        _userRepository = userRepository;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Dashboard", "Home");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null || !user.IsActive)
        {
            _audit.LogMinimal("auth.login", null, "failure", user: model.Email);
            ModelState.AddModelError("", "Credenziali non valide o account disattivato.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            _audit.LogMinimal("auth.login", null, "failure", user: model.Email);
            ModelState.AddModelError("", "Credenziali non valide o account disattivato.");
            return View(model);
        }

        _audit.LogMinimal("auth.login", $"user#{user.Id}", "success", user: user.Email);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Dashboard", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        _audit.LogMinimal("auth.logout", null, "success");
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View();

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        await _userManager.RemovePasswordAsync(user);
        var result = await _userManager.AddPasswordAsync(user, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        _audit.LogMinimal("auth.password_change", $"user#{user.Id} \"{user.Email}\"", "success");
        TempData["Success"] = "§account.msg_password_changed";
        return RedirectToAction("Dashboard", "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user != null && user.IsActive)
        {
            var token = Guid.NewGuid().ToString("N");
            var expiresAt = DateTime.UtcNow.AddHours(1);

            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                DELETE FROM password_reset_tokens WHERE user_id = @uid;
                INSERT INTO password_reset_tokens (user_id, token, expires_at)
                VALUES (@uid, @token, @exp);", conn);
            cmd.Parameters.AddWithValue("@uid", user.Id);
            cmd.Parameters.AddWithValue("@token", token);
            cmd.Parameters.AddWithValue("@exp", expiresAt);
            await cmd.ExecuteNonQueryAsync();

            var resetLink = Url.Action(
                "ResetLanding", "Account",
                new { token },
                Request.Scheme)!;

            try
            {
                await _emailService.SendPasswordResetEmailAsync(
                    user.Email ?? model.Email,
                    $"{user.FirstName} {user.LastName}",
                    resetLink);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", model.Email);
            }
        }

        TempData["ForgotPasswordSent"] = true;
        return RedirectToAction("ForgotPasswordConfirmation");
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return RedirectToAction("Login");

        var valid = await IsTokenValidAsync(token);
        if (!valid)
        {
            ViewBag.InvalidToken = true;
            return View(new ResetPasswordViewModel());
        }

        return View(new ResetPasswordViewModel { Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        try
        {
            using var selectCmd = new MySqlCommand(@"
                SELECT user_id FROM password_reset_tokens
                WHERE token = @token AND used = 0 AND expires_at > UTC_TIMESTAMP()
                LIMIT 1 FOR UPDATE", conn, tx);
            selectCmd.Parameters.AddWithValue("@token", model.Token);
            var userId = await selectCmd.ExecuteScalarAsync();

            if (userId == null)
            {
                await tx.RollbackAsync();
                ViewBag.InvalidToken = true;
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(userId.ToString()!);
            if (user == null)
            {
                await tx.RollbackAsync();
                ViewBag.InvalidToken = true;
                return View(model);
            }

            using var markCmd = new MySqlCommand(@"
                UPDATE password_reset_tokens SET used = 1
                WHERE token = @token AND used = 0", conn, tx);
            markCmd.Parameters.AddWithValue("@token", model.Token);
            var rowsAffected = await markCmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                await tx.RollbackAsync();
                ViewBag.InvalidToken = true;
                return View(model);
            }

            await tx.CommitAsync();

            var newHash = _userManager.PasswordHasher.HashPassword(user, model.NewPassword);
            user.PasswordHash = newHash;
            await _userManager.UpdateAsync(user);
            _audit.LogMinimal("auth.password_reset", $"user#{user.Id} \"{user.Email}\"", "success");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        TempData["ResetSuccess"] = true;
        return RedirectToAction("ResetPasswordConfirmation");
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation() => View();

    public IActionResult AccessDenied() => View();

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Dashboard", "Home");
        var rng = new Random();
        int a = rng.Next(1, 10), b = rng.Next(1, 10);
        HttpContext.Session.SetInt32("MathCaptchaAnswer", a + b);
        ViewBag.MathQuestion = $"{a} + {b}";
        return View(new PublicRegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(PublicRegisterViewModel model)
    {
        ModelState.Remove("MathCaptchaAnswer");

        var expected = HttpContext.Session.GetInt32("MathCaptchaAnswer");
        bool captchaOk = expected.HasValue
            && int.TryParse(model.MathCaptchaAnswer?.Trim(), out int submitted)
            && submitted == expected.Value;
        if (!captchaOk)
        {
            ModelState.AddModelError("MathCaptchaAnswer", "Risposta errata. Riprova.");
            // Regenerate question for retry
            var rng = new Random();
            int a = rng.Next(1, 10), b = rng.Next(1, 10);
            HttpContext.Session.SetInt32("MathCaptchaAnswer", a + b);
            ViewBag.MathQuestion = $"{a} + {b}";
            return View(model);
        }
        HttpContext.Session.Remove("MathCaptchaAnswer");

        if (!ModelState.IsValid)
        {
            var rng2 = new Random();
            int a2 = rng2.Next(1, 10), b2 = rng2.Next(1, 10);
            HttpContext.Session.SetInt32("MathCaptchaAnswer", a2 + b2);
            ViewBag.MathQuestion = $"{a2} + {b2}";
            return View(model);
        }

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing != null)
        {
            ModelState.AddModelError("Email", "Questa email è già registrata.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email    = model.Email,
            FirstName = model.FirstName.Trim(),
            LastName  = model.LastName.Trim(),
            IsActive  = true,
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return View(model);
        }

        // Nessun ruolo assegnato automaticamente — lo assegna l'amministratore

        // Fire-and-forget: benvenuto via email (non blocca la registrazione)
        _ = Task.Run(async () =>
        {
            try { await _emailService.SendRegistrationWelcomeAsync(user.Email!, $"{user.FirstName} {user.LastName}"); }
            catch { /* ignora errori email */ }
        });

        TempData["RegisterSuccess"] = true;
        return RedirectToAction("Login");
    }

    // ── SSO Login — avvia il flusso SAML verso l'IdP ─────────────────────
    [HttpGet]
    [AllowAnonymous]
    public IActionResult SsoLogin(string? returnUrl = null)
    {
        var redirectUri = Url.Action("SsoCallback", "Account", new { returnUrl });
        return Challenge(new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            RedirectUri = redirectUri
        }, "Saml2");
    }

    // ── SSO Callback — ricezione asserzione SAML ──────────────────────────
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> SsoCallback(string? returnUrl = null)
    {
        var result = await HttpContext.AuthenticateAsync(
            Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme);

        if (!result.Succeeded)
        {
            _logger.LogWarning("SSO callback failed: {Error}", result.Failure?.Message);
            TempData["Error"] = "§sso.error_auth";
            return RedirectToAction("Login");
        }

        var principal = result.Principal!;

        string? GetAttr(string friendly, string oid) =>
            principal.FindFirst(friendly)?.Value ?? principal.FindFirst(oid)?.Value;

        var mail = GetAttr("mail", "urn:oid:0.9.2342.19200300.100.1.3")
                   ?? principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var eppn = GetAttr("eduPersonPrincipalName", "urn:oid:1.3.6.1.4.1.5923.1.1.1.6");

        await HttpContext.SignOutAsync(
            Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme);

        if (string.IsNullOrEmpty(mail))
        {
            _logger.LogWarning("SSO callback: no mail attribute in assertion");
            TempData["Error"] = "§sso.error_no_mail";
            return RedirectToAction("Login");
        }

        // eppn è obbligatorio come identificativo stabile Shibboleth (fail-closed)
        if (string.IsNullOrEmpty(eppn))
        {
            _logger.LogWarning("SSO callback: eduPersonPrincipalName missing for mail={Mail}", mail);
            TempData["Error"] = "§sso.error_no_eppn";
            return RedirectToAction("Login");
        }

        var appUser = await _userManager.FindByEmailAsync(mail);
        if (appUser == null || !appUser.IsActive)
        {
            _audit.LogMinimal("auth.sso_login", null, "failure", user: mail);
            TempData["Error"] = "§sso.error_not_found";
            return RedirectToAction("Login");
        }

        // ── Identity binding hardening ────────────────────────────────────
        // Se l'utente ha già un shibboleth_id diverso dall'eppn in arrivo,
        // l'asserzione è per un'identità diversa: accesso negato per sicurezza.
        if (!string.IsNullOrEmpty(eppn)
            && !string.IsNullOrEmpty(appUser.ShibbolethId)
            && !string.Equals(appUser.ShibbolethId, eppn, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "SSO: eppn mismatch for user#{UserId} (stored='{Stored}' assertion='{Asserted}')",
                appUser.Id, appUser.ShibbolethId, eppn);
            _audit.LogMinimal("auth.sso_eppn_mismatch", $"user#{appUser.Id}",
                "failure", user: appUser.Email ?? mail);
            TempData["Error"] = "§sso.error_auth";
            return RedirectToAction("Login");
        }

        // Prima volta — collega eppn come identificativo stabile
        if (!string.IsNullOrEmpty(eppn) && string.IsNullOrEmpty(appUser.ShibbolethId))
        {
            try
            {
                await _userRepository.SetShibbolethIdAsync(appUser.Id, eppn);
                appUser.ShibbolethId = eppn;
            }
            catch (MySqlConnector.MySqlException ex)
                when (ex.Number == 1062) // ER_DUP_ENTRY — eppn già associato ad altro utente
            {
                _logger.LogWarning(
                    "SSO: eppn '{Eppn}' already linked to another user; denying access to user#{UserId}",
                    eppn, appUser.Id);
                _audit.LogMinimal("auth.sso_eppn_conflict", $"user#{appUser.Id}",
                    "failure", user: appUser.Email ?? mail);
                TempData["Error"] = "§sso.error_auth";
                return RedirectToAction("Login");
            }
        }

        await _signInManager.SignInAsync(appUser, isPersistent: false);
        _audit.LogMinimal("auth.sso_login", $"user#{appUser.Id}", "success", user: appUser.Email ?? mail);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Dashboard", "Home");
    }

    // ── Pending Role — utente registrato senza ruolo ─────────────────────
    [HttpGet]
    public IActionResult PendingRole() => View();

    // ── Reset Landing — pagina intermedia anti-bot ───────────────────────
    // I bot antispam eseguono solo GET sui link; questa pagina mostra solo
    // un pulsante che richiede un POST deliberato dall'utente.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ResetLanding(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return RedirectToAction("Login");

        var valid = await IsTokenValidAsync(token);
        if (!valid)
        {
            ViewBag.InvalidToken = true;
            return View();
        }

        ViewBag.Token = token;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult ResetLanding(ResetLandingPostModel model)
    {
        if (string.IsNullOrEmpty(model.Token))
            return RedirectToAction("Login");

        return RedirectToAction("ResetPassword", new { token = model.Token });
    }

    private async Task<bool> IsTokenValidAsync(string token)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT COUNT(*) FROM password_reset_tokens
            WHERE token = @token AND used = 0 AND expires_at > UTC_TIMESTAMP()", conn);
        cmd.Parameters.AddWithValue("@token", token);
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }
}
