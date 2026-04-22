using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BocconiLMS.Data;
using BocconiLMS.Models;

namespace BocconiLMS.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserRepository _users;
    private readonly CourseRepository _courses;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public AdminController(
        UserRepository users,
        CourseRepository courses,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _users = users;
        _courses = courses;
        _userManager = userManager;
        _roleManager = roleManager;
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
        user.Username = model.Username;
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

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
            await _roleManager.CreateAsync(new ApplicationRole(roleName));
    }
}
