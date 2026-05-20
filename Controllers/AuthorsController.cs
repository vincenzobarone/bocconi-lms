using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BocconiLMS.Data;
using BocconiLMS.Models;
using BocconiLMS.Services;

namespace BocconiLMS.Controllers;

[Authorize(Roles = "Admin")]
public class AuthorsController : Controller
{
    private readonly AuthorRepository _authors;
    private readonly TranslationService _t;
    private readonly IAuditLogger _audit;

    public AuthorsController(AuthorRepository authors, TranslationService t, IAuditLogger audit)
    {
        _authors = authors;
        _t       = t;
        _audit   = audit;
    }

    // ── Index ─────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index()
    {
        var list = await _authors.GetAllAsync();
        return View(list);
    }

    // ── Create ────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Create() => View(new Author());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Author model)
    {
        if (string.IsNullOrWhiteSpace(model.FullName))
            ModelState.AddModelError(nameof(model.FullName), _t.T("validation.required"));

        if (ModelState.IsValid && await _authors.NameExistsAsync(model.FullName))
            ModelState.AddModelError(nameof(model.FullName), _t.T("author.name_duplicate"));

        if (!ModelState.IsValid)
            return View(model);

        var id = await _authors.CreateAsync(model.FullName, model.Email, model.Affiliation);
        _audit.Log("author.create", $"author#{id} \"{model.FullName}\"");
        TempData["Success"] = $"§author.msg_created|{model.FullName}";
        return RedirectToAction(nameof(Index));
    }

    // ── Edit ──────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var author = await _authors.GetByIdAsync(id);
        if (author == null) return NotFound();
        return View(author);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Author model)
    {
        if (string.IsNullOrWhiteSpace(model.FullName))
            ModelState.AddModelError(nameof(model.FullName), _t.T("validation.required"));

        if (ModelState.IsValid && await _authors.NameExistsAsync(model.FullName, id))
            ModelState.AddModelError(nameof(model.FullName), _t.T("author.name_duplicate"));

        if (!ModelState.IsValid)
            return View(model);

        await _authors.UpdateAsync(id, model.FullName, model.Email, model.Affiliation);
        _audit.Log("author.edit", $"author#{id} \"{model.FullName}\"");
        TempData["Success"] = "§author.msg_updated";
        return RedirectToAction(nameof(Index));
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var author = await _authors.GetByIdAsync(id);
        if (author == null) return NotFound();

        if (author.MaterialCount > 0)
        {
            TempData["Error"] = $"§author.msg_delete_blocked|{author.FullName}|{author.MaterialCount}";
            return RedirectToAction(nameof(Index));
        }

        await _authors.DeleteAsync(id);
        _audit.Log("author.delete", $"author#{id} \"{author.FullName}\"");
        TempData["Success"] = $"§author.msg_deleted|{author.FullName}";
        return RedirectToAction(nameof(Index));
    }
}
