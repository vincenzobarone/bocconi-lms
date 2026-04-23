using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BocconiLMS.Data;
using BocconiLMS.Models;
using BocconiLMS.Services;

namespace BocconiLMS.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserRepository _users;
    private readonly CourseRepository _courses;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly SettingsRepository _settings;
    private readonly EmailService _emailService;
    private readonly TranslationRepository _translations;
    private readonly TranslationService _translationService;

    public AdminController(
        UserRepository users,
        CourseRepository courses,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        SettingsRepository settings,
        EmailService emailService,
        TranslationRepository translations,
        TranslationService translationService)
    {
        _users = users;
        _courses = courses;
        _userManager = userManager;
        _roleManager = roleManager;
        _settings = settings;
        _emailService = emailService;
        _translations = translations;
        _translationService = translationService;
    }

    public async Task<IActionResult> Index()
    {
        var stats = await _users.GetStatsAsync();
        return View(stats);
    }

    public async Task<IActionResult> Users()
    {
        var users = await _users.GetAllAsync();
        return View(users);
    }

    [HttpGet]
    public IActionResult CreateUser() => View(new RegisterViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (await _userManager.FindByEmailAsync(model.Email) != null)
        {
            ModelState.AddModelError("Email", "Email già in uso.");
            return View(model);
        }

        var validRoles = new[] { "Student", "Teacher", "Admin" };
        var role = validRoles.Contains(model.Role) ? model.Role : "Student";

        var appUser = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var result = await _userManager.CreateAsync(appUser, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError("", e.Description);
            return View(model);
        }

        await EnsureRoleExistsAsync(role);
        await _userManager.AddToRoleAsync(appUser, role);

        TempData["Success"] = $"Utente {appUser.FullName} creato con successo.";
        return RedirectToAction("Users");
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(int id)
    {
        var user = await _users.GetByIdAsync(id);
        if (user == null) return NotFound();
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(User model)
    {
        var user = await _users.GetByIdAsync(model.Id);
        if (user == null) return NotFound();
        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.Role = model.Role;
        user.IsActive = model.IsActive;
        await _users.UpdateAsync(user);

        var appUser = await _userManager.FindByIdAsync(model.Id.ToString());
        if (appUser != null)
        {
            var currentRoles = await _userManager.GetRolesAsync(appUser);
            await _userManager.RemoveFromRolesAsync(appUser, currentRoles);
            await EnsureRoleExistsAsync(model.Role);
            await _userManager.AddToRoleAsync(appUser, model.Role);
        }

        TempData["Success"] = "Utente aggiornato.";
        return RedirectToAction("Users");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(int id)
    {
        var user = await _users.GetByIdAsync(id);
        if (user == null) return NotFound();
        user.IsActive = !user.IsActive;
        await _users.UpdateAsync(user);
        TempData["Success"] = user.IsActive ? "Utente attivato." : "Utente disattivato.";
        return RedirectToAction("Users");
    }

    [HttpGet]
    public async Task<IActionResult> EmailSettings()
    {
        var current = await _emailService.GetEffectiveSettingsAsync();
        var vm = new EmailSettingsViewModel
        {
            Enabled   = current.Enabled,
            Host      = current.Host,
            Port      = current.Port,
            Username  = current.Username,
            FromEmail = current.FromEmail,
            FromName  = current.FromName,
            UseSsl    = current.UseSsl,
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailSettings(EmailSettingsViewModel model)
    {
        ModelState.Remove("TestEmailRecipient");
        if (!ModelState.IsValid) return View(model);

        try
        {
            await _settings.SetAsync("Smtp:Enabled",   model.Enabled.ToString().ToLower());
            await _settings.SetAsync("Smtp:Host",      model.Host ?? "");
            await _settings.SetAsync("Smtp:Port",      model.Port.ToString());
            await _settings.SetAsync("Smtp:Username",  model.Username ?? "");
            await _settings.SetAsync("Smtp:FromEmail", model.FromEmail ?? "");
            await _settings.SetAsync("Smtp:FromName",  model.FromName ?? "Bocconi LMS");
            await _settings.SetAsync("Smtp:UseSsl",    model.UseSsl.ToString().ToLower());

            if (!string.IsNullOrWhiteSpace(model.Password))
                await _settings.SetAsync("Smtp:Password", model.Password);

            TempData["Success"] = "Impostazioni email salvate con successo.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Errore nel salvataggio: {ex.Message}";
        }

        return RedirectToAction("EmailSettings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestEmail(EmailSettingsViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.TestEmailRecipient))
        {
            TempData["Error"] = "Inserire un indirizzo email per il test.";
            return RedirectToAction("EmailSettings");
        }

        try
        {
            var settings = await _emailService.GetEffectiveSettingsAsync();
            await _emailService.SendTestEmailAsync(model.TestEmailRecipient, settings);
            TempData["Success"] = $"Email di test inviata a {model.TestEmailRecipient}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Invio fallito: {ex.Message}";
        }

        return RedirectToAction("EmailSettings");
    }

    [HttpGet]
    public async Task<IActionResult> Translations()
    {
        var rows = await _translations.GetAllGroupedAsync();
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> EditTranslation(string key)
    {
        var row = await _translations.GetByKeyAsync(key);
        if (row == null) return NotFound();
        return View(row);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTranslation(TranslationRow model)
    {
        if (string.IsNullOrWhiteSpace(model.Key)) return BadRequest();
        await _translations.SaveRowAsync(model);
        _translationService.InvalidateCache();
        TempData["Success"] = $"Traduzioni per '{model.Key}' salvate.";
        return RedirectToAction("Translations");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTranslationKey(string key)
    {
        await _translations.DeleteKeyAsync(key);
        _translationService.InvalidateCache();
        TempData["Success"] = $"Chiave '{key}' eliminata.";
        return RedirectToAction("Translations");
    }

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
            await _roleManager.CreateAsync(new ApplicationRole(roleName));
    }
}
