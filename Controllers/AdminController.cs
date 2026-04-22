using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BocconiLMS.Data;
using BocconiLMS.Models;

namespace BocconiLMS.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserRepository _users;
    private readonly CourseRepository _courses;

    public AdminController(UserRepository users, CourseRepository courses)
    {
        _users = users;
        _courses = courses;
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
        if (await _users.EmailExistsAsync(model.Email))
        {
            ModelState.AddModelError("Email", "Email già in uso.");
            return View(model);
        }
        var user = new User
        {
            Username = model.Username,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            Role = model.Role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password)
        };
        await _users.CreateAsync(user);
        TempData["Success"] = $"Utente {user.FullName} creato con successo.";
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
}
