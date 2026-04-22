using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BocconiLMS.Data;
using BocconiLMS.Models;

namespace BocconiLMS.Controllers;

[Authorize]
public class DocumentController : Controller
{
    private readonly DocumentRepository _documents;
    private readonly LessonRepository _lessons;
    private readonly CourseRepository _courses;
    private readonly EnrollmentRepository _enrollments;
    private readonly IWebHostEnvironment _env;

    public DocumentController(DocumentRepository documents, LessonRepository lessons,
        CourseRepository courses, EnrollmentRepository enrollments, IWebHostEnvironment env)
    {
        _documents = documents;
        _lessons = lessons;
        _courses = courses;
        _enrollments = enrollments;
        _env = env;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string CurrentRole => User.FindFirst(ClaimTypes.Role)!.Value;

    private async Task<bool> IsOwnerOrAdminOfCourseAsync(int courseId)
    {
        if (CurrentRole == "Admin") return true;
        var course = await _courses.GetByIdAsync(courseId);
        return course != null && course.TeacherId == CurrentUserId;
    }

    private async Task<(Lesson? lesson, int courseId)> GetLessonCourseAsync(int lessonId)
    {
        var lesson = await _lessons.GetByIdAsync(lessonId);
        return (lesson, lesson?.CourseId ?? 0);
    }

    private async Task<(Lesson? lesson, int courseId, Document? doc)> GetDocContextAsync(int documentId)
    {
        var doc = await _documents.GetByIdAsync(documentId);
        if (doc == null) return (null, 0, null);
        var lesson = await _lessons.GetByIdAsync(doc.LessonId);
        return (lesson, lesson?.CourseId ?? 0, doc);
    }

    private async Task<IActionResult?> RequireDocumentAccessAsync(int documentId)
    {
        var (lesson, courseId, doc) = await GetDocContextAsync(documentId);
        if (doc == null || lesson == null) return NotFound();

        if (CurrentRole == "Admin") return null;

        if (CurrentRole == "Teacher")
        {
            if (!await IsOwnerOrAdminOfCourseAsync(courseId)) return Forbid();
            return null;
        }

        if (!lesson.IsPublished) return Forbid();
        if (!await _enrollments.IsEnrolledAsync(CurrentUserId, courseId)) return Forbid();
        return null;
    }

    public async Task<IActionResult> Details(int id)
    {
        var access = await RequireDocumentAccessAsync(id);
        if (access != null) return access;

        var doc = await _documents.GetByIdAsync(id);
        if (doc == null) return NotFound();
        var versions = await _documents.GetVersionsAsync(id);
        ViewBag.Versions = versions;
        var docCtx = await GetDocContextAsync(id);
        ViewBag.IsOwner = await IsOwnerOrAdminOfCourseAsync(docCtx.courseId);
        return View(doc);
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpGet]
    public async Task<IActionResult> Upload(int lessonId, int? documentId = null)
    {
        var (lesson, courseId) = await GetLessonCourseAsync(lessonId);
        if (lesson == null) return NotFound();
        if (!await IsOwnerOrAdminOfCourseAsync(courseId)) return Forbid();

        Document? existingDoc = null;
        if (documentId.HasValue)
        {
            existingDoc = await _documents.GetByIdAsync(documentId.Value);
            if (existingDoc != null && existingDoc.LessonId != lessonId) return Forbid();
        }

        var model = new DocumentUploadViewModel
        {
            LessonId = lessonId,
            DocumentId = documentId,
            Title = existingDoc?.Title ?? ""
        };
        ViewBag.LessonTitle = lesson.Title;
        ViewBag.ExistingDoc = existingDoc;
        return View(model);
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(DocumentUploadViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var (lesson, courseId) = await GetLessonCourseAsync(model.LessonId);
        if (lesson == null) return NotFound();
        if (!await IsOwnerOrAdminOfCourseAsync(courseId)) return Forbid();

        if (model.File == null || model.File.Length == 0)
        {
            ModelState.AddModelError("File", "Seleziona un file.");
            return View(model);
        }

        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".txt" };
        var ext = Path.GetExtension(model.File.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            ModelState.AddModelError("File", "Tipo file non supportato.");
            return View(model);
        }

        int docId;
        if (model.DocumentId.HasValue && model.DocumentId.Value > 0)
        {
            var existingDoc = await _documents.GetByIdAsync(model.DocumentId.Value);
            if (existingDoc == null || existingDoc.LessonId != model.LessonId) return Forbid();
            docId = model.DocumentId.Value;
        }
        else
        {
            docId = await _documents.CreateDocumentAsync(model.Title, model.LessonId);
        }

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", docId.ToString());
        Directory.CreateDirectory(uploadsDir);
        var nextVersion = await _documents.GetNextVersionNumberAsync(docId);
        var safeFileName = $"v{nextVersion}_{Path.GetFileNameWithoutExtension(model.File.FileName)}{ext}";
        var filePath = Path.Combine(uploadsDir, safeFileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
            await model.File.CopyToAsync(stream);

        var version = new DocumentVersion
        {
            DocumentId = docId,
            VersionNumber = nextVersion,
            FileName = model.File.FileName,
            FilePath = $"/uploads/{docId}/{safeFileName}",
            FileType = ext.TrimStart('.').ToUpperInvariant(),
            FileSizeBytes = model.File.Length,
            UploadedBy = CurrentUserId,
            Notes = model.Notes
        };
        await _documents.AddVersionAsync(version);

        TempData["Success"] = $"Documento caricato (versione {nextVersion}).";
        return RedirectToAction("Details", "Lesson", new { id = model.LessonId });
    }

    public async Task<IActionResult> Download(int versionId)
    {
        var version = await _documents.GetVersionByIdAsync(versionId);
        if (version == null) return NotFound();

        var access = await RequireDocumentAccessAsync(version.DocumentId);
        if (access != null) return access;

        var physPath = Path.Combine(_env.WebRootPath, version.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(physPath)) return NotFound();
        var mimeType = version.FileType.ToLower() switch
        {
            "pdf" => "application/pdf",
            "doc" or "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "ppt" or "pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            _ => "application/octet-stream"
        };
        return PhysicalFile(physPath, mimeType, version.FileName);
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int documentId, int versionId)
    {
        var (_, courseId, doc) = await GetDocContextAsync(documentId);
        if (doc == null) return NotFound();
        if (!await IsOwnerOrAdminOfCourseAsync(courseId)) return Forbid();
        await _documents.RestoreVersionAsync(documentId, versionId);
        TempData["Success"] = "Versione ripristinata.";
        return RedirectToAction("Details", new { id = documentId });
    }
}
