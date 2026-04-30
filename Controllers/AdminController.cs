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
    private readonly EnrollmentRepository _enrollments;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly SettingsRepository _settings;
    private readonly EmailService _emailService;
    private readonly TranslationRepository _translations;
    private readonly TranslationService _translationService;
    private readonly DocumentTypeRepository _docTypes;
    private readonly FeatureFlagService _features;
    private readonly AreaRepository _areas;
    private readonly RolePermissionRepository _rolePerms;

    public AdminController(
        UserRepository users,
        CourseRepository courses,
        EnrollmentRepository enrollments,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        SettingsRepository settings,
        EmailService emailService,
        TranslationRepository translations,
        TranslationService translationService,
        DocumentTypeRepository docTypes,
        FeatureFlagService features,
        AreaRepository areas,
        RolePermissionRepository rolePerms)
    {
        _users = users;
        _courses = courses;
        _enrollments = enrollments;
        _userManager = userManager;
        _roleManager = roleManager;
        _settings = settings;
        _emailService = emailService;
        _translations = translations;
        _translationService = translationService;
        _docTypes = docTypes;
        _features = features;
        _areas = areas;
        _rolePerms = rolePerms;
    }

    public IActionResult Index()
    {
        return RedirectToAction("Dashboard", "Home");
    }

    [AllowAnonymous]
    public async Task<IActionResult> Users(string? tab)
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        var activeTab = tab is "ruoli" ? tab : "utenti";
        var vm = new UsersAndRolesViewModel
        {
            Users    = await _users.GetAllAsync(),
            Roles    = await _users.GetAllRolesWithCountAsync(),
            Areas    = await _areas.GetAllAsync(),
            ActiveTab = activeTab
        };
        ViewBag.CoursesEnabled = await _features.IsCoursesEnabledAsync();
        return View(vm);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> CreateUser()
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        ViewBag.AvailableRoles = await _users.GetNonAdminRoleNamesAsync();
        return View(new RegisterViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(RegisterViewModel model)
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        var availableRoles = await _users.GetNonAdminRoleNamesAsync();
        if (!ModelState.IsValid)
        {
            ViewBag.AvailableRoles = availableRoles;
            return View(model);
        }

        if (await _userManager.FindByEmailAsync(model.Email) != null)
        {
            ModelState.AddModelError("Email", "Email già in uso.");
            ViewBag.AvailableRoles = availableRoles;
            return View(model);
        }

        var role = availableRoles.Contains(model.Role) ? model.Role : availableRoles.FirstOrDefault() ?? "";

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

        var creatorId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _users.SetUserCreatedByAsync(appUser.Id, creatorId);

        TempData["Success"] = $"Utente {appUser.FullName} creato con successo.";
        return RedirectToAction("Users");
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> EditUser(int id)
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        var user = await _users.GetByIdAsync(id);
        if (user == null) return NotFound();
        var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        if (user.Id == currentUserId || user.Role == "Admin")
        {
            TempData["Error"] = "Non puoi modificare questo utente.";
            return RedirectToAction(nameof(Users));
        }
        ViewBag.AvailableRoles  = await _users.GetNonAdminRoleNamesAsync();
        ViewBag.AllAreas        = await _areas.GetAllAsync();
        ViewBag.UserAreaIds     = await _areas.GetUserAreaIdsAsync(id);
        return View(user);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(User model, List<int>? areaIds)
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        var user = await _users.GetByIdAsync(model.Id);
        if (user == null) return NotFound();
        var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        if (user.Id == currentUserId || user.Role == "Admin")
        {
            TempData["Error"] = "Non puoi modificare questo utente.";
            return RedirectToAction(nameof(Users));
        }
        user.FirstName = model.FirstName;
        user.LastName = model.LastName;

        var nonAdminRoles = await _users.GetNonAdminRoleNamesAsync();
        string resolvedRole = user.Role;
        if (user.Role != "Admin")
        {
            var requestedRole = nonAdminRoles.Contains(model.Role) ? model.Role : user.Role;

            if (requestedRole != user.Role)
            {
                // Block docente → * if they have active courses
                if (user.CanTeach)
                {
                    var courseCount = await _users.GetActiveCourseCountAsync(user.Id);
                    if (courseCount > 0)
                    {
                        ModelState.AddModelError("Role",
                            $"Cannot change role: this teacher has {courseCount} active course(s). Reassign or delete the courses first.");
                        ViewBag.AvailableRoles = nonAdminRoles;
                        return View(user);
                    }
                }
                // Block studente → * if they are enrolled in any course
                else if (user.CanAttend)
                {
                    var enrollments = await _enrollments.GetByUserAsync(user.Id);
                    if (enrollments.Count > 0)
                    {
                        ModelState.AddModelError("Role",
                            $"Cannot change role: this student is enrolled in {enrollments.Count} course(s). Unenroll them first.");
                        ViewBag.AvailableRoles = nonAdminRoles;
                        return View(user);
                    }
                }
                resolvedRole = requestedRole;
            }

            user.Role = resolvedRole;
        }

        // Block deactivating the last active admin via edit form
        if (user.Role == "Admin" && !model.IsActive)
        {
            var activeAdmins = await _users.CountActiveAdminsAsync();
            if (activeAdmins <= 1)
            {
                TempData["Error"] = "Impossibile disattivare l'unico amministratore attivo.";
                return RedirectToAction("Users");
            }
        }

        user.IsActive = model.IsActive;
        await _users.UpdateAsync(user);

        var appUser = await _userManager.FindByIdAsync(model.Id.ToString());
        if (appUser != null && user.Role != "Admin")
        {
            var currentRoles = await _userManager.GetRolesAsync(appUser);
            await _userManager.RemoveFromRolesAsync(appUser, currentRoles);
            await EnsureRoleExistsAsync(resolvedRole);
            await _userManager.AddToRoleAsync(appUser, resolvedRole);
        }

        await _areas.SetUserAreasAsync(model.Id, areaIds ?? new List<int>());

        TempData["Success"] = "Utente aggiornato.";
        return RedirectToAction("Users");
    }

    // ── Area management ───────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateArea(string name)
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
        {
            TempData["Error"] = "Nome area non valido.";
            return RedirectToAction("Dictionary", new { tab = "aree" });
        }
        if (await _areas.NameExistsAsync(name))
        {
            TempData["Error"] = $"Un'area con il nome «{name}» esiste già.";
            return RedirectToAction("Dictionary", new { tab = "aree" });
        }
        var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _areas.CreateAsync(name, currentUserId);
        TempData["Success"] = $"Area «{name.Trim()}» creata.";
        return RedirectToAction("Dictionary", new { tab = "aree" });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditArea(int id, string name)
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
        {
            TempData["Error"] = "Nome area non valido.";
            return RedirectToAction("Dictionary", new { tab = "aree" });
        }
        if (await _areas.NameExistsAsync(name, excludeId: id))
        {
            TempData["Error"] = $"Un'area con il nome «{name.Trim()}» esiste già.";
            return RedirectToAction("Dictionary", new { tab = "aree" });
        }
        await _areas.RenameAsync(id, name);
        TempData["Success"] = $"Area rinominata in «{name.Trim()}».";
        return RedirectToAction("Dictionary", new { tab = "aree" });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteArea(int id)
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        var count = await _areas.CountUsersAsync(id);
        if (count > 0)
        {
            TempData["Error"] = $"Impossibile eliminare: {count} utente/i ha questa area.";
            return RedirectToAction("Dictionary", new { tab = "aree" });
        }
        await _areas.DeleteAsync(id);
        TempData["Success"] = "Area eliminata.";
        return RedirectToAction("Dictionary", new { tab = "aree" });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        var user = await _users.GetByIdAsync(id);
        if (user == null) return NotFound();

        var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        if (user.Id == currentUserId)
        {
            TempData["Error"] = "Cannot delete your own account.";
            return RedirectToAction("Users");
        }

        if (user.CanTeach)
        {
            var courseCount = await _users.GetActiveCourseCountAsync(id);
            if (courseCount > 0)
            {
                TempData["Error"] = $"Cannot delete teacher \"{user.FullName}\": they have {courseCount} active course(s). Reassign or delete the courses first.";
                return RedirectToAction("Users");
            }
        }

        var appUser = await _userManager.FindByIdAsync(id.ToString());
        await _users.DeleteWithCascadeAsync(id);
        if (appUser != null)
            await _userManager.DeleteAsync(appUser);

        TempData["Success"] = $"User \"{user.FullName}\" deleted.";
        return RedirectToAction("Users");
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(int id)
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        var user = await _users.GetByIdAsync(id);
        if (user == null) return NotFound();

        // Block deactivating the last active admin
        if (user.Role == "Admin" && user.IsActive)
        {
            var activeAdmins = await _users.CountActiveAdminsAsync();
            if (activeAdmins <= 1)
            {
                TempData["Error"] = "Impossibile disattivare l'unico amministratore attivo.";
                return RedirectToAction("Users");
            }
        }

        user.IsActive = !user.IsActive;
        await _users.UpdateAsync(user);
        TempData["Success"] = user.IsActive ? "Utente attivato." : "Utente disattivato.";
        return RedirectToAction("Users");
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> UserCourses(int id)
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        var user = await _users.GetByIdAsync(id);
        if (user == null) return NotFound();

        object courses;
        if (user.CanTeach)
        {
            var list = await _courses.GetByTeacherAsync(id);
            courses = list.Select(c => new { c.Id, c.Title, c.IsPublished, Type = "taught" });
        }
        else if (user.CanAttend)
        {
            var list = await _enrollments.GetByUserAsync(id);
            courses = list.Select(e => new
            {
                Id    = e.CourseId,
                Title = e.CourseTitle,
                IsPublished = true,
                Type  = "enrolled",
                e.TotalLessons,
                e.CompletedLessons
            });
        }
        else
        {
            courses = Array.Empty<object>();
        }

        return Json(new { user.FullName, user.Role, courses });
    }

    [HttpGet]
    public async Task<IActionResult> EmailSettings()
    {
        var current = await _emailService.GetEffectiveSettingsAsync();

        // Migration: se le nuove chiavi non sono ancora impostate, eredita dalla vecchia chiave unificata
        var oldEnabled  = (await _settings.GetAsync("Notifications:MaterialChanged")) == "true";
        var oldRolesRaw = await _settings.GetAsync("Notifications:MaterialChangedRoles") ?? "";

        var createdEnabledRaw = await _settings.GetAsync("Notifications:MaterialCreated");
        var createdRolesRaw   = await _settings.GetAsync("Notifications:MaterialCreatedRoles");
        var updatedEnabledRaw = await _settings.GetAsync("Notifications:MaterialUpdated");
        var updatedRolesRaw   = await _settings.GetAsync("Notifications:MaterialUpdatedRoles");
        var deletedEnabledRaw = await _settings.GetAsync("Notifications:MaterialDeleted");
        var deletedRolesRaw   = await _settings.GetAsync("Notifications:MaterialDeletedRoles");

        // Se le nuove chiavi non esistono ancora, inizializza dai valori vecchi
        bool createdEnabled = createdEnabledRaw != null ? createdEnabledRaw == "true" : oldEnabled;
        bool updatedEnabled = updatedEnabledRaw != null ? updatedEnabledRaw == "true" : oldEnabled;
        bool deletedEnabled = deletedEnabledRaw == "true";
        var createdRoles = (createdRolesRaw ?? (createdEnabledRaw == null ? oldRolesRaw : ""))
            .Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        var updatedRoles = (updatedRolesRaw ?? (updatedEnabledRaw == null ? oldRolesRaw : ""))
            .Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        var deletedRoles = (deletedRolesRaw ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        var vm = new EmailSettingsViewModel
        {
            Enabled   = current.Enabled,
            Host      = current.Host,
            Port      = current.Port,
            Username  = current.Username,
            Password  = current.Password,
            FromEmail = current.FromEmail,
            FromName  = current.FromName,
            UseSsl    = current.UseSsl,
            NotifyMaterialCreated  = createdEnabled,
            MaterialCreatedRoles   = createdRoles,
            NotifyMaterialUpdated  = updatedEnabled,
            MaterialUpdatedRoles   = updatedRoles,
            NotifyMaterialDeleted  = deletedEnabled,
            MaterialDeletedRoles   = deletedRoles,
            AvailableRoles               = (await _users.GetAllRolesWithCountAsync()).Select(r => r.Name).ToList(),
            CoursesNotificationsEnabled  = (await _settings.GetAsync("Notifications:CoursesEnabled")) == "true",
            NotifyStudentOnEnroll        = (await _settings.GetAsync("Notifications:StudentOnEnroll")) == "true",
            NotifyStudentOnQuizCompleted = (await _settings.GetAsync("Notifications:StudentOnQuizCompleted")) == "true",
            NotifyTeacherOnQuizCompleted = (await _settings.GetAsync("Notifications:TeacherOnQuizCompleted")) == "true",
            NotifyTeacherOnStudentEnrolled = (await _settings.GetAsync("Notifications:TeacherOnStudentEnrolled")) == "true",
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailSettings(EmailSettingsViewModel model)
    {
        ModelState.Remove("TestEmailRecipient");
        ModelState.Remove("Password");
        ModelState.Remove("AvailableRoles");
        if (!ModelState.IsValid)
        {
            model.AvailableRoles = (await _users.GetAllRolesWithCountAsync()).Select(r => r.Name).ToList();
            return View(model);
        }

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

            await _settings.SetAsync("Notifications:MaterialCreated",
                model.NotifyMaterialCreated ? "true" : "false");
            await _settings.SetAsync("Notifications:MaterialCreatedRoles",
                string.Join(",", model.MaterialCreatedRoles ?? new List<string>()));
            await _settings.SetAsync("Notifications:MaterialUpdated",
                model.NotifyMaterialUpdated ? "true" : "false");
            await _settings.SetAsync("Notifications:MaterialUpdatedRoles",
                string.Join(",", model.MaterialUpdatedRoles ?? new List<string>()));
            await _settings.SetAsync("Notifications:MaterialDeleted",
                model.NotifyMaterialDeleted ? "true" : "false");
            await _settings.SetAsync("Notifications:MaterialDeletedRoles",
                string.Join(",", model.MaterialDeletedRoles ?? new List<string>()));

            await _settings.SetAsync("Notifications:CoursesEnabled",
                model.CoursesNotificationsEnabled ? "true" : "false");
            await _settings.SetAsync("Notifications:StudentOnEnroll",
                model.NotifyStudentOnEnroll ? "true" : "false");
            await _settings.SetAsync("Notifications:StudentOnQuizCompleted",
                model.NotifyStudentOnQuizCompleted ? "true" : "false");
            await _settings.SetAsync("Notifications:TeacherOnQuizCompleted",
                model.NotifyTeacherOnQuizCompleted ? "true" : "false");
            await _settings.SetAsync("Notifications:TeacherOnStudentEnrolled",
                model.NotifyTeacherOnStudentEnrolled ? "true" : "false");

            TempData["Success"] = _translationService.T("admin.email.saved", "Impostazioni email salvate con successo.");
        }
        catch (Exception ex)
        {
            TempData["Error"] = string.Format(
                _translationService.T("admin.email.save_error", "Errore nel salvataggio: {0}"),
                ex.Message);
        }

        return RedirectToAction("EmailSettings");
    }

    // ── Toggle email/notify — endpoint AJAX (risposta JSON) ─────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleEmailSendingAjax([FromBody] AjaxToggleRequest req)
    {
        await _settings.SetAsync("Smtp:Enabled", req.Value ? "true" : "false");
        return Json(new { ok = true });
    }

    // ── AJAX: notifiche materiale Create ─────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleNotifyMaterialCreatedAjax([FromBody] AjaxToggleRequest req)
    {
        await _settings.SetAsync("Notifications:MaterialCreated", req.Value ? "true" : "false");
        if (!req.Value) await _settings.SetAsync("Notifications:MaterialCreatedRoles", "");
        return Json(new { ok = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetNotifyCreatedRolesAjax([FromBody] AjaxRolesRequest req)
    {
        await _settings.SetAsync("Notifications:MaterialCreatedRoles",
            string.Join(",", req.Roles ?? new List<string>()));
        return Json(new { ok = true });
    }

    // ── AJAX: notifiche materiale Update ─────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleNotifyMaterialUpdatedAjax([FromBody] AjaxToggleRequest req)
    {
        await _settings.SetAsync("Notifications:MaterialUpdated", req.Value ? "true" : "false");
        if (!req.Value) await _settings.SetAsync("Notifications:MaterialUpdatedRoles", "");
        return Json(new { ok = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetNotifyUpdatedRolesAjax([FromBody] AjaxRolesRequest req)
    {
        await _settings.SetAsync("Notifications:MaterialUpdatedRoles",
            string.Join(",", req.Roles ?? new List<string>()));
        return Json(new { ok = true });
    }

    // ── AJAX: notifiche materiale Delete ─────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleNotifyMaterialDeletedAjax([FromBody] AjaxToggleRequest req)
    {
        await _settings.SetAsync("Notifications:MaterialDeleted", req.Value ? "true" : "false");
        if (!req.Value) await _settings.SetAsync("Notifications:MaterialDeletedRoles", "");
        return Json(new { ok = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetNotifyDeletedRolesAjax([FromBody] AjaxRolesRequest req)
    {
        await _settings.SetAsync("Notifications:MaterialDeletedRoles",
            string.Join(",", req.Roles ?? new List<string>()));
        return Json(new { ok = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCoursesNotificationsAjax([FromBody] AjaxToggleRequest req)
    {
        await _settings.SetAsync("Notifications:CoursesEnabled", req.Value ? "true" : "false");
        if (!req.Value)
        {
            await _settings.SetAsync("Notifications:StudentOnEnroll", "false");
            await _settings.SetAsync("Notifications:StudentOnQuizCompleted", "false");
            await _settings.SetAsync("Notifications:TeacherOnQuizCompleted", "false");
            await _settings.SetAsync("Notifications:TeacherOnStudentEnrolled", "false");
        }
        return Json(new { ok = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCourseNotifyItemAjax([FromBody] AjaxCourseNotifyRequest req)
    {
        var key = req.Item switch
        {
            "StudentOnEnroll"          => "Notifications:StudentOnEnroll",
            "StudentOnQuizCompleted"   => "Notifications:StudentOnQuizCompleted",
            "TeacherOnQuizCompleted"   => "Notifications:TeacherOnQuizCompleted",
            "TeacherOnStudentEnrolled" => "Notifications:TeacherOnStudentEnrolled",
            _ => null
        };
        if (key == null) return Json(new { ok = false });
        await _settings.SetAsync(key, req.Value ? "true" : "false");
        return Json(new { ok = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestEmail(EmailSettingsViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.TestEmailRecipient))
        {
            TempData["Error"] = _translationService.T("admin.email.test_no_recipient", "Inserire un indirizzo email per il test.");
            return RedirectToAction("EmailSettings");
        }

        try
        {
            var settings = await _emailService.GetEffectiveSettingsAsync();
            await _emailService.SendTestEmailAsync(model.TestEmailRecipient, settings);
            TempData["Success"] = string.Format(
                _translationService.T("admin.email.test_sent", "Email di test inviata a {0}."),
                model.TestEmailRecipient);
        }
        catch (Exception ex)
        {
            TempData["Error"] = string.Format(
                _translationService.T("admin.email.test_failed", "Invio fallito: {0}"),
                ex.Message);
        }

        return RedirectToAction("EmailSettings");
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Translations()
    {
        if (!await CanAccessMenuAsync("menu.translations")) return Forbid();
        return RedirectToAction(nameof(Dictionary));
    }

    [AllowAnonymous]
    public async Task<IActionResult> Dictionary(string? tab)
    {
        if (!await CanAccessMenuAsync("menu.translations")) return Forbid();
        var rows = await _translations.GetAllGroupedAsync();
        ViewBag.EnabledLanguages  = await _settings.GetEnabledLanguagesAsync();
        ViewBag.MissingCounts     = await _translations.GetMissingCountsAsync();
        ViewBag.DocTypes          = await _docTypes.GetAllAsync();
        ViewBag.Areas             = await _areas.GetAllAsync();
        ViewBag.ActiveTab         = tab is "doctypes" or "aree" ? tab : "translations";
        return View(rows);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLanguageSettings(List<string> enabledLanguages)
    {
        if (!await CanAccessMenuAsync("menu.translations")) return Forbid();
        if (enabledLanguages == null || !enabledLanguages.Contains("en"))
            enabledLanguages = (enabledLanguages ?? new()) .Prepend("en").ToList();
        await _settings.SaveEnabledLanguagesAsync(enabledLanguages);
        _translationService.InvalidateCache();
        TempData["Success"] = "Language settings saved.";
        return RedirectToAction(nameof(PlatformFeatures));
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FillMissingTranslations()
    {
        if (!await CanAccessMenuAsync("menu.translations")) return Forbid();
        var enabled = await _settings.GetEnabledLanguagesAsync();
        var count = await _translations.FillMissingAsync(enabled.Where(l => l != "en"));
        _translationService.InvalidateCache();
        TempData["Success"] = $"Filled {count} missing translation(s) with English defaults.";
        return RedirectToAction(nameof(Dictionary));
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> EditTranslation(string key)
    {
        if (!await CanAccessMenuAsync("menu.translations")) return Forbid();
        var row = await _translations.GetByKeyAsync(key);
        if (row == null) return NotFound();
        ViewBag.EnabledLanguages = await _settings.GetEnabledLanguagesAsync();
        return View(row);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTranslation(TranslationRow model)
    {
        if (!await CanAccessMenuAsync("menu.translations")) return Forbid();
        if (string.IsNullOrWhiteSpace(model.Key)) return BadRequest();
        await _translations.SaveRowAsync(model);
        _translationService.InvalidateCache();
        TempData["Success"] = $"Traduzioni per '{model.Key}' salvate.";
        return RedirectToAction(nameof(Dictionary));
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTranslationKey(string key)
    {
        if (!await CanAccessMenuAsync("menu.translations")) return Forbid();
        await _translations.DeleteKeyAsync(key);
        _translationService.InvalidateCache();
        TempData["Success"] = $"Chiave '{key}' eliminata.";
        return RedirectToAction(nameof(Dictionary));
    }

    // ── ROLE MANAGEMENT ─────────────────────────────────────────────────────

    [AllowAnonymous]
    public async Task<IActionResult> Roles()
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        var roles = await _users.GetAllRolesWithCountAsync();
        return View(roles);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> CreateRole()
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        ViewBag.CoursesEnabled = await _features.IsCoursesEnabledAsync();
        return View(new RoleFormViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRole(RoleFormViewModel model, List<string>? permissions)
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        if (!ModelState.IsValid)
        {
            ViewBag.CoursesEnabled = await _features.IsCoursesEnabledAsync();
            model.Permissions = permissions ?? new List<string>();
            return View(model);
        }
        var name = model.Name.Trim();
        if (name.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("Name", "Il ruolo Admin è protetto e non può essere creato manualmente.");
            ViewBag.CoursesEnabled = await _features.IsCoursesEnabledAsync();
            model.Permissions = permissions ?? new List<string>();
            return View(model);
        }
        if (await _roleManager.RoleExistsAsync(name))
        {
            ModelState.AddModelError("Name", $"Esiste già un ruolo con il nome '{name}'.");
            ViewBag.CoursesEnabled = await _features.IsCoursesEnabledAsync();
            model.Permissions = permissions ?? new List<string>();
            return View(model);
        }
        var role = new ApplicationRole { Name = name, NormalizedName = name.ToUpperInvariant(), CanTeach = model.CanTeach, CanAttend = model.CanAttend };
        await _roleManager.CreateAsync(role);
        var created = await _roleManager.FindByNameAsync(name);
        if (created != null)
        {
            if (permissions?.Count > 0)
                await _rolePerms.SetRolePermissionsAsync(created.Id, permissions);
            var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            await _users.SetRoleCreatedByAsync(created.Id, currentUserId);
        }
        TempData["Success"] = $"Ruolo '{name}' creato con successo.";
        return RedirectToAction(nameof(Users), new { tab = "ruoli" });
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> EditRole(int id)
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null) return NotFound();
        if (role.Name!.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Il ruolo Admin è protetto e non può essere modificato.";
            return RedirectToAction(nameof(Users), new { tab = "ruoli" });
        }
        var perms = await _rolePerms.GetRolePermissionsAsync(role.Id);
        ViewBag.CoursesEnabled = await _features.IsCoursesEnabledAsync();
        return View(new RoleFormViewModel { Id = role.Id, Name = role.Name!, Permissions = perms, CanTeach = role.CanTeach, CanAttend = role.CanAttend });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRole(RoleFormViewModel model, List<string>? permissions)
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        var role = await _roleManager.FindByIdAsync(model.Id.ToString());
        if (role == null) return NotFound();
        if (role.Name!.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Il ruolo Admin è protetto e non può essere modificato.";
            return RedirectToAction(nameof(Users), new { tab = "ruoli" });
        }
        if (!ModelState.IsValid)
        {
            ViewBag.CoursesEnabled = await _features.IsCoursesEnabledAsync();
            model.Permissions = permissions ?? new();
            return View(model);
        }

        model.Name = model.Name.Trim();
        if (model.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("Name", "Non è possibile rinominare un ruolo 'Admin'.");
            ViewBag.CoursesEnabled = await _features.IsCoursesEnabledAsync();
            model.Permissions = permissions ?? new();
            return View(model);
        }
        var existing = await _roleManager.FindByNameAsync(model.Name.ToUpperInvariant());
        if (existing != null && existing.Id != model.Id)
        {
            ModelState.AddModelError("Name", $"Esiste già un ruolo con il nome '{model.Name}'.");
            ViewBag.CoursesEnabled = await _features.IsCoursesEnabledAsync();
            model.Permissions = permissions ?? new();
            return View(model);
        }
        role.Name = model.Name;
        role.NormalizedName = model.Name.ToUpperInvariant();
        role.CanTeach = model.CanTeach;
        role.CanAttend = model.CanAttend;
        await _roleManager.UpdateAsync(role);
        await _rolePerms.SetRolePermissionsAsync(role.Id, permissions ?? new());
        TempData["Success"] = string.Format(
            _translationService.T("admin.role_updated", "Ruolo aggiornato in '{0}'."),
            model.Name);
        return RedirectToAction(nameof(Users), new { tab = "ruoli" });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRole(int id)
    {
        if (!await CanAccessMenuAsync("menu.users")) return Forbid();
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null) return NotFound();
        if (role.Name!.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Il ruolo Admin è protetto e non può essere eliminato.";
            return RedirectToAction(nameof(Users), new { tab = "ruoli" });
        }
        var userCount = await _users.CountUsersInRoleAsync(role.Id);
        if (userCount > 0)
        {
            TempData["Error"] = $"Impossibile eliminare '{role.Name}': {userCount} utente/i ha questo ruolo. Riassegna prima gli utenti.";
            return RedirectToAction(nameof(Users), new { tab = "ruoli" });
        }
        await _roleManager.DeleteAsync(role);
        TempData["Success"] = $"Ruolo '{role.Name}' eliminato.";
        return RedirectToAction(nameof(Users), new { tab = "ruoli" });
    }

    // ── Document Types ────────────────────────────────────────────────────

    [AllowAnonymous]
    public IActionResult DocumentTypes()
    {
        return RedirectToAction(nameof(Dictionary), new { tab = "doctypes" });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDocumentType(DocumentTypeFormViewModel vm)
    {
        if (!await CanAccessMenuAsync("menu.translations")) return Forbid();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Nome non valido.";
            return RedirectToAction(nameof(Dictionary), new { tab = "doctypes" });
        }
        if (await _docTypes.NameExistsAsync(vm.Name))
        {
            TempData["Error"] = $"Esiste già un tipo chiamato '{vm.Name}'.";
            return RedirectToAction(nameof(Dictionary), new { tab = "doctypes" });
        }
        await _docTypes.CreateAsync(vm.Name);
        TempData["Success"] = $"Tipo '{vm.Name}' creato.";
        return RedirectToAction(nameof(Dictionary), new { tab = "doctypes" });
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> EditDocumentType(int id)
    {
        if (!await CanAccessMenuAsync("menu.translations")) return Forbid();
        var t = await _docTypes.GetByIdAsync(id);
        if (t == null) return NotFound();
        return View(new DocumentTypeFormViewModel { Id = t.Id, Name = t.Name });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDocumentType(int id, DocumentTypeFormViewModel vm)
    {
        if (!await CanAccessMenuAsync("menu.translations")) return Forbid();
        if (!ModelState.IsValid) return View(vm);
        if (await _docTypes.NameExistsAsync(vm.Name, id))
        {
            ModelState.AddModelError(nameof(vm.Name), $"Esiste già un tipo chiamato '{vm.Name}'.");
            return View(vm);
        }
        await _docTypes.UpdateAsync(id, vm.Name);
        TempData["Success"] = "Tipo documento aggiornato.";
        return RedirectToAction(nameof(Dictionary), new { tab = "doctypes" });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocumentType(int id)
    {
        if (!await CanAccessMenuAsync("menu.translations")) return Forbid();
        var count = await _docTypes.CountMaterialsAsync(id);
        if (count > 0)
        {
            TempData["Error"] = $"Impossibile eliminare: {count} materiale/i usa questo tipo.";
            return RedirectToAction(nameof(Dictionary), new { tab = "doctypes" });
        }
        await _docTypes.DeleteAsync(id);
        TempData["Success"] = "Tipo documento eliminato.";
        return RedirectToAction(nameof(Dictionary), new { tab = "doctypes" });
    }

    // ── Platform Features ─────────────────────────────────────────────────

    public async Task<IActionResult> PlatformFeatures()
    {
        ViewBag.CoursesEnabled    = await _features.IsCoursesEnabledAsync();
        ViewBag.MaterialsEnabled  = await _features.IsMaterialsEnabledAsync();
        ViewBag.EnabledLanguages  = await _settings.GetEnabledLanguagesAsync();
        ViewBag.MissingCounts     = await _translations.GetMissingCountsAsync();
        ViewBag.PlatformTimezone  = await _settings.GetAsync("Platform:Timezone") ?? "Europe/Rome";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("Admin/PlatformFeatures/SaveTimezone")]
    public async Task<IActionResult> SaveTimezone(string timezone)
    {
        if (!string.IsNullOrWhiteSpace(timezone))
            await _settings.SetAsync("Platform:Timezone", timezone.Trim());
        TempData["Success"] = "Fuso orario aggiornato.";
        return RedirectToAction(nameof(PlatformFeatures));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlatformFeatures(bool coursesEnabled)
    {
        await _features.SetCoursesEnabledAsync(coursesEnabled);
        TempData["Success"] = coursesEnabled
            ? "Modulo Corsi abilitato."
            : "Modulo Corsi disabilitato.";
        return RedirectToAction(nameof(PlatformFeatures));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("Admin/PlatformFeatures/ToggleMaterials")]
    public async Task<IActionResult> ToggleMaterials(bool materialsEnabled)
    {
        await _features.SetMaterialsEnabledAsync(materialsEnabled);
        TempData["Success"] = materialsEnabled
            ? "Modulo Materiali abilitato."
            : "Modulo Materiali disabilitato.";
        return RedirectToAction(nameof(PlatformFeatures));
    }

    private async Task<bool> CanAccessMenuAsync(string permission)
    {
        if (User.Identity?.IsAuthenticated != true) return false;
        if (User.IsInRole("Admin")) return true;
        var roleName = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
        return await _rolePerms.HasMenuPermissionAsync(roleName, permission);
    }

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
            await _roleManager.CreateAsync(new ApplicationRole(roleName));
    }
}
