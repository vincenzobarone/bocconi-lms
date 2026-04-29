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

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        DbHelper db,
        EmailService emailService,
        ILogger<AccountController> logger,
        IConfiguration config)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _emailService = emailService;
        _logger = logger;
        _config = config;
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
            ModelState.AddModelError("", "Credenziali non valide o account disattivato.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Credenziali non valide o account disattivato.");
            return View(model);
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault();

        if (string.IsNullOrEmpty(role))
            return RedirectToAction("PendingRole");

        return role switch
        {
            "Admin"   => RedirectToAction("Index", "Admin"),
            "Teacher" => RedirectToAction("Dashboard", "Course"),
            _         => RedirectToAction("Dashboard", "Student")
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
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
        TempData["Success"] = "Password aggiornata con successo.";
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
