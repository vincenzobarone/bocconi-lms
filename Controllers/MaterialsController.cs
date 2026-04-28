using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BocconiLMS.Data;
using BocconiLMS.Models;
using System.Security.Claims;

namespace BocconiLMS.Controllers;

[Authorize]
public class MaterialsController : Controller
{
    private readonly MaterialRepository _materials;
    private readonly DocumentTypeRepository _docTypes;
    private readonly UserRepository _users;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<MaterialsController> _logger;

    public MaterialsController(
        MaterialRepository materials,
        DocumentTypeRepository docTypes,
        UserRepository users,
        IWebHostEnvironment env,
        ILogger<MaterialsController> logger)
    {
        _materials = materials;
        _docTypes  = docTypes;
        _users     = users;
        _env       = env;
        _logger    = logger;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private int CurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task PopulateDropdownsAsync()
    {
        ViewBag.DocumentTypes   = await _docTypes.GetAllAsync();
        ViewBag.Languages       = Material.Languages;
        ViewBag.AvailableOwners = await _users.GetTeachersAndAdminsAsync();
        ViewBag.ExistingAuthors = await _materials.GetDistinctAuthorsAsync();
    }

    private string? CurrentUserFullName() =>
        User.FindFirstValue("FullName") ?? User.Identity?.Name;

    private static async Task<string?> TryExtractAuthorAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        try
        {
            if (ext is ".docx" or ".pptx" or ".xlsx")
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                ms.Position = 0;
                using var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
                var entry = zip.GetEntry("docProps/core.xml");
                if (entry != null)
                {
                    using var sr = new StreamReader(entry.Open());
                    var xml = await sr.ReadToEndAsync();
                    var m = System.Text.RegularExpressions.Regex.Match(xml, @"<dc:creator>(.*?)</dc:creator>");
                    if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                        return m.Groups[1].Value.Trim();
                }
            }
            else if (ext == ".pdf")
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var bytes = ms.ToArray();
                var text = System.Text.Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, 8192));
                var m = System.Text.RegularExpressions.Regex.Match(text, @"/Author\s*\(([^)]+)\)");
                if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                    return m.Groups[1].Value.Trim();
            }
        }
        catch { }
        return null;
    }

    // ── Index ─────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index(
        string? q = null,
        string? lang = null,
        int? typeId = null)
    {
        var materials = await _materials.GetAllAsync(q, lang, typeId);
        var vm = new MaterialsIndexViewModel
        {
            Materials      = materials,
            SearchTitle    = q,
            FilterLanguage = lang,
            FilterTypeId   = typeId,
            DocumentTypes  = await _docTypes.GetAllAsync()
        };
        return View(vm);
    }

    // ── Details (version history) ─────────────────────────────────────────

    public async Task<IActionResult> Details(int id)
    {
        var material = await _materials.GetByIdAsync(id);
        if (material == null) return NotFound();
        var versions = await _materials.GetVersionsAsync(id);
        ViewBag.Versions = versions;
        return View(material);
    }

    // ── Create ────────────────────────────────────────────────────────────

    [Authorize(Roles = "Teacher,Admin")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        ViewBag.CurrentUserFullName = CurrentUserFullName();
        var vm = new MaterialFormViewModel { Language = "Italiano" };
        return View(vm);
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MaterialFormViewModel vm)
    {
        // Owner is always the current logged-in user on create
        vm.OwnerId = CurrentUserId();

        // Try to extract author from document metadata if field is empty
        if (string.IsNullOrWhiteSpace(vm.AuthorName) && vm.File != null)
        {
            vm.AuthorName = await TryExtractAuthorAsync(vm.File);
            if (!string.IsNullOrWhiteSpace(vm.AuthorName))
                ModelState.Remove(nameof(vm.AuthorName));
        }

        if (string.IsNullOrWhiteSpace(vm.AuthorName))
            ModelState.AddModelError(nameof(vm.AuthorName), "L'autore è obbligatorio.");

        if (vm.File == null || vm.File.Length == 0)
            ModelState.AddModelError(nameof(vm.File), "Il file è obbligatorio.");

        if (!ModelState.IsValid)
        {
            ViewBag.CurrentUserFullName = CurrentUserFullName();
            await PopulateDropdownsAsync();
            return View(vm);
        }

        if (await _materials.TitleExistsAsync(vm.Title))
        {
            ModelState.AddModelError(nameof(vm.Title), "Esiste già un materiale con questo titolo.");
            ViewBag.CurrentUserFullName = CurrentUserFullName();
            await PopulateDropdownsAsync();
            return View(vm);
        }

        var matId = await _materials.CreateAsync(vm.Title, vm.AuthorName, vm.OwnerId, vm.Language, vm.DocumentTypeId, vm.Status, vm.Folder);

        if (vm.File != null && vm.File.Length > 0)
        {
            try
            {
                await SaveVersionAsync(matId, vm.File, vm.Notes, vm.ConvertToPdf);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveVersionAsync fallita per materialId={MatId} file={File}", matId, vm.File.FileName);
                TempData["Warning"] = $"Materiale creato, ma il file non è stato salvato: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        TempData["Success"] = $"Materiale «{vm.Title}» creato con successo.";
        return RedirectToAction(nameof(Index));
    }

    // ── Edit ──────────────────────────────────────────────────────────────

    [Authorize(Roles = "Teacher,Admin")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var material = await _materials.GetByIdAsync(id);
        if (material == null) return NotFound();
        await PopulateDropdownsAsync();
        var vm = new MaterialFormViewModel
        {
            Id             = material.Id,
            Title          = material.Title,
            AuthorName     = material.AuthorName,
            OwnerId        = material.OwnerId,
            Language       = material.Language,
            DocumentTypeId = material.DocumentTypeId,
            Status         = material.Status,
            Folder         = material.Folder
        };
        ViewBag.Material = material;
        return View(vm);
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MaterialFormViewModel vm)
    {
        // Try to extract author from document metadata if field is empty
        if (string.IsNullOrWhiteSpace(vm.AuthorName) && vm.File != null)
        {
            vm.AuthorName = await TryExtractAuthorAsync(vm.File);
            if (!string.IsNullOrWhiteSpace(vm.AuthorName))
                ModelState.Remove(nameof(vm.AuthorName));
        }

        if (string.IsNullOrWhiteSpace(vm.AuthorName))
            ModelState.AddModelError(nameof(vm.AuthorName), "L'autore è obbligatorio.");

        if (!ModelState.IsValid)
        {
            var mat = await _materials.GetByIdAsync(id);
            ViewBag.Material = mat;
            await PopulateDropdownsAsync();
            return View(vm);
        }

        if (await _materials.TitleExistsAsync(vm.Title, id))
        {
            ModelState.AddModelError(nameof(vm.Title), "Esiste già un materiale con questo titolo.");
            var mat = await _materials.GetByIdAsync(id);
            ViewBag.Material = mat;
            await PopulateDropdownsAsync();
            return View(vm);
        }

        await _materials.UpdateAsync(id, vm.Title, vm.AuthorName, vm.OwnerId, vm.Language, vm.DocumentTypeId, vm.Status, vm.Folder);

        if (vm.File != null && vm.File.Length > 0)
        {
            try
            {
                await SaveVersionAsync(id, vm.File, vm.Notes, vm.ConvertToPdf);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveVersionAsync fallita in Edit per materialId={Id} file={File}", id, vm.File.FileName);
                TempData["Warning"] = $"Materiale aggiornato, ma il file non è stato salvato: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        TempData["Success"] = "Materiale aggiornato.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── Upload new version (from Details page) ────────────────────────────

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadVersion(int id, IFormFile file, string? notes)
    {
        var material = await _materials.GetByIdAsync(id);
        if (material == null) return NotFound();
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Seleziona un file da caricare.";
            return RedirectToAction(nameof(Details), new { id });
        }
        try
        {
            await SaveVersionAsync(id, file, notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveVersionAsync fallita in UploadVersion per materialId={Id} file={File}", id, file.FileName);
            TempData["Error"] = $"Errore nel salvataggio del file: {ex.Message}";
            return RedirectToAction(nameof(Details), new { id });
        }
        TempData["Success"] = "Nuova versione caricata.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── Restore version ───────────────────────────────────────────────────

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int materialId, int versionId)
    {
        var material = await _materials.GetByIdAsync(materialId);
        if (material == null) return NotFound();
        await _materials.RestoreVersionAsync(materialId, versionId);
        TempData["Success"] = "Versione ripristinata.";
        return RedirectToAction(nameof(Details), new { id = materialId });
    }

    // ── Download ──────────────────────────────────────────────────────────

    [Authorize]
    public async Task<IActionResult> Download(int versionId)
    {
        var version = await _materials.GetVersionByIdAsync(versionId);
        if (version == null) return NotFound();
        var fullPath = Path.Combine(_env.WebRootPath, version.FilePath.TrimStart('/'));
        if (!System.IO.File.Exists(fullPath)) return NotFound();
        return PhysicalFile(fullPath, "application/octet-stream", version.FileName);
    }

    // ── Preview (inline, no download) ────────────────────────────────────

    [Authorize]
    public async Task<IActionResult> Preview(int versionId)
    {
        var version = await _materials.GetVersionByIdAsync(versionId);
        if (version == null) return NotFound();
        var fullPath = Path.Combine(_env.WebRootPath, version.FilePath.TrimStart('/'));
        if (!System.IO.File.Exists(fullPath)) return NotFound();
        var mime = GetMimeType(version.FileType);
        return PhysicalFile(fullPath, mime, enableRangeProcessing: true);
    }

    private static string GetMimeType(string fileType) => fileType.ToUpperInvariant() switch
    {
        "PDF"          => "application/pdf",
        "PNG"          => "image/png",
        "JPG" or "JPEG"=> "image/jpeg",
        "GIF"          => "image/gif",
        "SVG"          => "image/svg+xml",
        "BMP"          => "image/bmp",
        "WEBP"         => "image/webp",
        "MP4"          => "video/mp4",
        "WEBM"         => "video/webm",
        "MOV"          => "video/quicktime",
        "AVI"          => "video/x-msvideo",
        "MKV"          => "video/x-matroska",
        _              => "application/octet-stream"
    };

    // ── Delete ────────────────────────────────────────────────────────────

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var material = await _materials.GetByIdAsync(id);
        if (material == null) return NotFound();

        var versions = await _materials.GetVersionsAsync(id);
        foreach (var v in versions)
        {
            var fullPath = Path.Combine(_env.WebRootPath, v.FilePath.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }

        var dirPath = Path.Combine(_env.WebRootPath, "uploads", "mat_" + id);
        if (Directory.Exists(dirPath)) Directory.Delete(dirPath, true);

        await _materials.DeleteAsync(id);
        TempData["Success"] = $"Materiale «{material.Title}» eliminato.";
        return RedirectToAction(nameof(Index));
    }

    // ── Lesson integration ────────────────────────────────────────────────

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkToLesson(int lessonId, int materialId)
    {
        await _materials.LinkToLessonAsync(lessonId, materialId, CurrentUserId());
        TempData["Success"] = "Materiale collegato alla lezione.";
        return RedirectToAction("Details", "Lesson", new { id = lessonId });
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlinkFromLesson(int lessonId, int materialId)
    {
        await _materials.UnlinkFromLessonAsync(lessonId, materialId);
        TempData["Success"] = "Materiale rimosso dalla lezione.";
        return RedirectToAction("Details", "Lesson", new { id = lessonId });
    }

    // ── Private: save file ────────────────────────────────────────────────

    private async Task SaveVersionAsync(int materialId, IFormFile file, string? notes,
                                         bool convertToPdf = false)
    {
        IFormFile fileToSave = file;
        if (convertToPdf)
        {
            var converted = await TryConvertToPdfAsync(file);
            if (converted != null) fileToSave = converted;
        }

        var nextVer  = await _materials.GetNextVersionNumberAsync(materialId);
        var ext      = Path.GetExtension(fileToSave.FileName).TrimStart('.').ToUpperInvariant();
        var safeFile = $"v{nextVer}_{Path.GetFileNameWithoutExtension(fileToSave.FileName)}{Path.GetExtension(fileToSave.FileName)}";
        var relDir   = Path.Combine("uploads", $"mat_{materialId}");
        var absDir   = Path.Combine(_env.WebRootPath, relDir);
        Directory.CreateDirectory(absDir);
        var absPath  = Path.Combine(absDir, safeFile);
        using (var fs = new FileStream(absPath, FileMode.Create))
            await fileToSave.CopyToAsync(fs);

        await _materials.AddVersionAsync(new MaterialVersion
        {
            MaterialId      = materialId,
            VersionNumber   = nextVer,
            FileName        = fileToSave.FileName,
            FilePath        = "/" + relDir.Replace('\\', '/') + "/" + safeFile,
            FileType        = ext,
            FileSizeBytes   = fileToSave.Length,
            UploadedBy      = CurrentUserId(),
            Notes           = notes
        });
    }

    private static readonly HashSet<string> _officeExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".doc", ".docx", ".ppt", ".pptx" };

    private async Task<IFormFile?> TryConvertToPdfAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName);
        if (!_officeExtensions.Contains(ext)) return null;

        var sofficePath = FindSoffice();
        if (sofficePath == null) return null;

        var tmpDir = Path.Combine(Path.GetTempPath(), $"lms_pdf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            var srcPath = Path.Combine(tmpDir, file.FileName);
            await using (var fs = System.IO.File.Create(srcPath))
                await file.CopyToAsync(fs);

            using var proc = new System.Diagnostics.Process();
            proc.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = sofficePath,
                Arguments              = $"--headless --convert-to pdf --outdir \"{tmpDir}\" \"{srcPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            proc.Start();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0) return null;

            var pdfName = Path.GetFileNameWithoutExtension(file.FileName) + ".pdf";
            var pdfPath = Path.Combine(tmpDir, pdfName);
            if (!System.IO.File.Exists(pdfPath)) return null;

            var ms = new MemoryStream(await System.IO.File.ReadAllBytesAsync(pdfPath));
            return new FormFile(ms, 0, ms.Length, file.Name, pdfName)
            {
                Headers     = new HeaderDictionary(),
                ContentType = "application/pdf"
            };
        }
        catch
        {
            return null;
        }
        finally
        {
            try { Directory.Delete(tmpDir, true); } catch { }
        }
    }

    private static string? FindSoffice()
    {
        foreach (var fixedPath in new[] { "/usr/bin/soffice", "/usr/local/bin/soffice" })
            if (System.IO.File.Exists(fixedPath)) return fixedPath;

        try
        {
            using var p = System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName               = "which",
                    Arguments              = "soffice",
                    RedirectStandardOutput = true,
                    UseShellExecute        = false
                });
            p?.WaitForExit(3000);
            var path = p?.StandardOutput.ReadToEnd().Trim();
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path)) return path;
        }
        catch { }

        return null;
    }
}
