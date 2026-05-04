using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BocconiLMS.Data;
using BocconiLMS.Models;
using BocconiLMS.Services;
using System.Security.Claims;
using System.IO.Compression;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BocconiLMS.Controllers;

[Authorize]
public class MaterialsController : Controller
{
    private readonly MaterialRepository _materials;
    private readonly DocumentTypeRepository _docTypes;
    private readonly UserRepository _users;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<MaterialsController> _logger;
    private readonly AreaRepository _areas;
    private readonly PlatformRepository _platforms;
    private readonly RolePermissionRepository _rolePerms;
    private readonly SettingsRepository _settings;
    private readonly EmailService _emailService;
    private readonly FeatureFlagService _features;
    private readonly TranslationService _t;
    private readonly IAuditLogger _audit;

    public MaterialsController(
        MaterialRepository materials,
        DocumentTypeRepository docTypes,
        UserRepository users,
        IWebHostEnvironment env,
        ILogger<MaterialsController> logger,
        AreaRepository areas,
        PlatformRepository platforms,
        RolePermissionRepository rolePerms,
        SettingsRepository settings,
        EmailService emailService,
        FeatureFlagService features,
        TranslationService t,
        IAuditLogger audit)
    {
        _materials    = materials;
        _docTypes     = docTypes;
        _users        = users;
        _env          = env;
        _logger       = logger;
        _areas        = areas;
        _platforms    = platforms;
        _rolePerms    = rolePerms;
        _settings     = settings;
        _emailService = emailService;
        _features     = features;
        _t            = t;
        _audit        = audit;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private int CurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // eventType: "created" | "updated" | "deleted"
    private void FireMaterialNotification(string materialTitle, string eventType)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var settingKey = eventType switch
                {
                    "created" => "Notifications:MaterialCreated",
                    "updated" => "Notifications:MaterialUpdated",
                    "deleted" => "Notifications:MaterialDeleted",
                    _         => "Notifications:MaterialCreated"
                };
                var rolesKey = eventType switch
                {
                    "created" => "Notifications:MaterialCreatedRoles",
                    "updated" => "Notifications:MaterialUpdatedRoles",
                    "deleted" => "Notifications:MaterialDeletedRoles",
                    _         => "Notifications:MaterialCreatedRoles"
                };
                if ((await _settings.GetAsync(settingKey)) != "true") return;
                var raw   = await _settings.GetAsync(rolesKey) ?? "";
                var roles = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (roles.Length == 0) return;

                var recipients = await _users.GetDistinctRecipientsByRoleNamesAsync(roles);
                foreach (var (email, fullName) in recipients)
                {
                    try { await _emailService.SendMaterialNotificationAsync(email, fullName, materialTitle, eventType); }
                    catch { /* singolo destinatario fallito: ignora e continua */ }
                }
            }
            catch { /* errore globale: non blocca l'operazione principale */ }
        });
    }

    private string CurrentRoleName() =>
        User.FindFirst(ClaimTypes.Role)?.Value ?? "";

    private async Task<bool> CanSetStatusAsync(string operation)
    {
        if (User.IsInRole("Admin")) return true;
        return await _rolePerms.HasMenuPermissionAsync(CurrentRoleName(), $"materials.{operation}.setstatus");
    }

    private async Task<bool> CanCreateMaterialAsync()
    {
        if (User.IsInRole("Admin") || User.IsInRole("CanTeach")) return true;
        return await _rolePerms.HasMenuPermissionAsync(CurrentRoleName(), "materials.create");
    }

    private async Task<bool> CanEditMaterialAsync()
    {
        if (User.IsInRole("Admin") || User.IsInRole("CanTeach")) return true;
        return await _rolePerms.HasMenuPermissionAsync(CurrentRoleName(), "materials.edit");
    }

    private async Task<bool> CanPublishMaterialAsync()
    {
        if (User.IsInRole("Admin")) return true;
        return await _rolePerms.HasMenuPermissionAsync(CurrentRoleName(), "materials.publish");
    }

    private async Task PopulateDropdownsAsync()
    {
        ViewBag.DocumentTypes   = await _docTypes.GetAllAsync();
        ViewBag.Languages       = Material.Languages;
        ViewBag.AvailableOwners = await _users.GetTeachersAndAdminsAsync();
        ViewBag.ExistingAuthors = await _materials.GetDistinctAuthorsAsync();
        ViewBag.ExistingFolders = await _materials.GetAllFoldersAsync();
        ViewBag.Platforms       = await _platforms.GetAllAsync();
        // Areas: Admin sees all, Teacher sees only their assigned areas
        ViewBag.AvailableAreas = User.IsInRole("Admin")
            ? await _areas.GetAllAsync()
            : await _areas.GetUserAreasAsync(CurrentUserId());
    }

    // ── AJAX: next protocol number ─────────────────────────────────────────

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> NextProtocol()
    {
        if (!await CanCreateMaterialAsync()) return Forbid();
        var next = await _materials.GetNextProtocolNumberAsync();
        return Json(new { protocol = next });
    }

    // ── AJAX: cerca titoli simili ──────────────────────────────────────────

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> CheckSimilarTitles(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length < 3)
            return Json(Array.Empty<object>());
        var results = await _materials.SearchSimilarTitlesAsync(title.Trim());
        return Json(results.Select(m => new { id = m.Id, title = m.Title, status = m.Status }));
    }

    // ── AJAX: list existing folders ────────────────────────────────────────

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Folders()
    {
        if (!await CanCreateMaterialAsync()) return Forbid();
        var folders = await _materials.GetAllFoldersAsync();
        return Json(folders.Select(f => new { f.Id, f.Name }));
    }

    private string? CurrentUserFullName() =>
        User.FindFirstValue("FullName") ?? User.Identity?.Name;

    /// <summary>
    /// Replaces the DataAnnotation-generated [Required] error on DocumentTypeId
    /// with the translated version from the translation system.
    /// </summary>
    private void TranslateDocTypeError(string fieldName, int? value)
    {
        if (ModelState.ContainsKey(fieldName))
        {
            ModelState.Remove(fieldName);
        }
        if (!value.HasValue || value == 0)
        {
            ModelState.AddModelError(fieldName,
                _t.T("mat.doctype_required"));
        }
    }

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
                var text = System.Text.Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, 65536));
                // PDF 1.x info dict: /Author (John Doe)
                var m = System.Text.RegularExpressions.Regex.Match(text, @"/Author\s*\(([^)\\]*(?:\\.[^)\\]*)*)\)");
                if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                    return m.Groups[1].Value.Trim();
                // XMP metadata: <dc:creator><rdf:Bag><rdf:li>author</rdf:li>
                var mXmp = System.Text.RegularExpressions.Regex.Match(text, @"<dc:creator[\s\S]*?<rdf:li[^>]*>([\s\S]*?)<\/rdf:li>");
                if (mXmp.Success && !string.IsNullOrWhiteSpace(mXmp.Groups[1].Value))
                    return mXmp.Groups[1].Value.Trim();
            }
        }
        catch { }
        return null;
    }

    private static async Task<int?> TryExtractPageCountFromPdfAsync(IFormFile file)
    {
        try
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();
            var text = System.Text.Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, 65536));
            // Look for /Type /Pages ... /Count N (pages tree root)
            var mPages = System.Text.RegularExpressions.Regex.Match(text,
                @"/Type\s*/Pages[\s\S]{0,200}?/Count\s+(\d+)");
            if (mPages.Success) return int.Parse(mPages.Groups[1].Value);
            // Fallback: find all /Count values, take the largest (root pages node)
            var allCounts = System.Text.RegularExpressions.Regex.Matches(text, @"/Count\s+(\d+)");
            if (allCounts.Count > 0)
                return allCounts.Cast<System.Text.RegularExpressions.Match>()
                                .Max(c => int.Parse(c.Groups[1].Value));
        }
        catch { }
        return null;
    }

    // ── Index ─────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index(
        string? q = null,
        string? lang = null,
        int? typeId = null,
        int? catYear = null,
        int? modYear = null,
        string? folderName = null,
        int? folderId = null)
    {
        if (!User.IsInRole("Admin") && !await _features.IsMaterialsEnabledAsync())
            return RedirectToAction("NoModules", "Home");

        var materials = await _materials.GetAllAsync(q, lang, typeId, catYear, modYear, folderName, folderId);
        ViewBag.CanCreate = await CanCreateMaterialAsync();
        ViewBag.CanEdit   = await CanEditMaterialAsync();
        var vm = new MaterialsIndexViewModel
        {
            Materials             = materials,
            SearchTitle           = q,
            FilterLanguage        = lang,
            FilterTypeId          = typeId,
            FilterCatalogationYear = catYear,
            FilterModifiedYear    = modYear,
            FilterFolderName      = folderName,
            FilterFolderId        = folderId,
            DocumentTypes         = await _docTypes.GetAllAsync()
        };
        return View(vm);
    }

    // ── Export Excel ──────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> ExportExcel(
        string? q = null, string? lang = null, int? typeId = null,
        int? catYear = null, int? modYear = null,
        string? folderName = null, int? folderId = null)
    {
        var materials = await _materials.GetAllAsync(q, lang, typeId, catYear, modYear, folderName, folderId);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Materiali");

        // Intestazioni
        string[] headers = ["#", "Titolo", "Autore", "Lingua", "Tipo documento", "Stato",
                             "Area", "Cartella", "N. Protocollo", "Data catalogazione", "Data creazione"];
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#003366");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Dati
        int row = 2;
        foreach (var m in materials)
        {
            ws.Cell(row, 1).Value  = m.Id;
            ws.Cell(row, 2).Value  = m.Title;
            ws.Cell(row, 3).Value  = m.AuthorName ?? "";
            ws.Cell(row, 4).Value  = m.Language;
            ws.Cell(row, 5).Value  = m.DocumentTypeName;
            ws.Cell(row, 6).Value  = m.Status;
            ws.Cell(row, 7).Value  = m.AreaName;
            ws.Cell(row, 8).Value  = m.FolderName;
            if (m.ProtocolNumber.HasValue) ws.Cell(row, 9).Value = m.ProtocolNumber.Value;
            else ws.Cell(row, 9).Value = "";
            ws.Cell(row, 10).Value = m.CatalogationDate.HasValue
                ? m.CatalogationDate.Value.ToString("dd/MM/yyyy") : "";
            ws.Cell(row, 11).Value = m.CreatedAt.ToString("dd/MM/yyyy");

            // Zebra stripes
            if (row % 2 == 0)
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#f5f7fa");
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.Column(2).Width = Math.Min(ws.Column(2).Width, 60); // cap titolo

        // Freeze intestazione e autofilter
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed()!.SetAutoFilter();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var fileName = $"materiali_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    // ── Export PDF ────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> ExportPdf(
        string? q = null, string? lang = null, int? typeId = null,
        int? catYear = null, int? modYear = null,
        string? folderName = null, int? folderId = null)
    {
        var materials = await _materials.GetAllAsync(q, lang, typeId, catYear, modYear, folderName, folderId);

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(ts => ts.FontSize(9).FontFamily("Arial"));

                // Header
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Didasco – Libreria Materiali")
                            .FontSize(14).Bold().FontColor(Color.FromHex("003366"));
                        row.ConstantItem(120).AlignRight().Text(
                            $"Esportato il {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(8).FontColor(Color.FromHex("666666"));
                    });
                    col.Item().PaddingTop(2).Text(
                        $"{materials.Count} materiali – Filtri: " +
                        string.Join(", ", new[]
                        {
                            q != null ? $"titolo={q}" : null,
                            lang != null ? $"lingua={lang}" : null,
                            catYear.HasValue ? $"anno cat.={catYear}" : null,
                            modYear.HasValue ? $"anno mod.={modYear}" : null,
                            folderName != null ? $"cartella={folderName}" : null
                        }.Where(x => x != null))
                    ).FontSize(8).FontColor(Color.FromHex("888888"));
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Color.FromHex("003366"));
                });

                // Footer
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Pagina ").FontSize(8);
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" di ").FontSize(8);
                    t.TotalPages().FontSize(8);
                });

                // Tabella
                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(30);   // #
                        cols.RelativeColumn(3);    // Titolo
                        cols.RelativeColumn(2);    // Autore
                        cols.RelativeColumn(1.2f); // Lingua
                        cols.RelativeColumn(1.8f); // Tipo
                        cols.RelativeColumn(1.2f); // Stato
                        cols.RelativeColumn(1.5f); // Cartella
                        cols.RelativeColumn(1.5f); // Data cat.
                    });

                    IContainer Hcell(IContainer c) =>
                        c.Background(Color.FromHex("003366")).Padding(4);

                    table.Header(h =>
                    {
                        foreach (var hdr in new[] { "#", "Titolo", "Autore", "Lingua", "Tipo documento", "Stato", "Cartella", "Data cat." })
                        {
                            var label = hdr;
                            h.Cell().Element(Hcell).Text(label)
                                .FontColor(Colors.White).Bold().FontSize(8);
                        }
                    });

                    // Data rows
                    int idx = 0;
                    foreach (var m in materials)
                    {
                        var bg = idx % 2 == 0 ? Colors.White : Color.FromHex("f5f7fa");

                        IContainer Dcell(IContainer c) =>
                            c.Background(bg).BorderBottom(0.5f).BorderColor(Color.FromHex("dddddd")).Padding(3);

                        table.Cell().Element(Dcell).Text($"{m.Id}").FontSize(8);
                        table.Cell().Element(Dcell).Text(m.Title).FontSize(8);
                        table.Cell().Element(Dcell).Text(m.AuthorName ?? "—").FontSize(8);
                        table.Cell().Element(Dcell).Text(m.Language).FontSize(8);
                        table.Cell().Element(Dcell).Text(m.DocumentTypeName).FontSize(8);
                        table.Cell().Element(Dcell).Text(m.Status).FontSize(8);
                        table.Cell().Element(Dcell).Text(m.FolderName).FontSize(8);
                        table.Cell().Element(Dcell).Text(
                            m.CatalogationDate.HasValue ? m.CatalogationDate.Value.ToString("dd/MM/yyyy") : "—"
                        ).FontSize(8);

                        idx++;
                    }
                });
            });
        }).GeneratePdf();

        var fileName = $"materiali_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    // ── Details (version history) ─────────────────────────────────────────

    public async Task<IActionResult> Details(int id)
    {
        var material = await _materials.GetByIdAsync(id);
        if (material == null) return NotFound();
        var versions = await _materials.GetVersionsAsync(id);
        ViewBag.Versions = versions;
        ViewBag.CanEdit  = await CanEditMaterialAsync();
        return View(material);
    }

    // ── Create ────────────────────────────────────────────────────────────

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!await CanCreateMaterialAsync()) return Forbid();
        await PopulateDropdownsAsync();
        ViewBag.CurrentUserFullName = CurrentUserFullName();
        ViewBag.CanSetStatus  = await CanSetStatusAsync("create");
        ViewBag.CanPublish    = await CanPublishMaterialAsync();
        var vm = new MaterialFormViewModel { Language = "Italiano", CatalogationDate = DateTime.Today };
        return View(vm);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MaterialFormViewModel vm)
    {
        if (!await CanCreateMaterialAsync()) return Forbid();
        // Owner is always the current logged-in user on create
        vm.OwnerId = CurrentUserId();

        // Try to extract author from document metadata if field is empty
        if (string.IsNullOrWhiteSpace(vm.AuthorName) && vm.File != null)
        {
            vm.AuthorName = await TryExtractAuthorAsync(vm.File);
            if (!string.IsNullOrWhiteSpace(vm.AuthorName))
                ModelState.Remove(nameof(vm.AuthorName));
        }

        // Try to extract page count server-side from PDF when JS didn't provide it
        if (!vm.PageCount.HasValue && vm.File != null &&
            Path.GetExtension(vm.File.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            vm.PageCount = await TryExtractPageCountFromPdfAsync(vm.File);
        }

        if (string.IsNullOrWhiteSpace(vm.AuthorName))
            ModelState.AddModelError(nameof(vm.AuthorName), _t.T("mat.author_required"));

        if (vm.File == null || vm.File.Length == 0)
            ModelState.AddModelError(nameof(vm.File), _t.T("mat.file_required"));

        TranslateDocTypeError(nameof(vm.DocumentTypeId), vm.DocumentTypeId);

        if (!ModelState.IsValid)
        {
            ViewBag.CurrentUserFullName = CurrentUserFullName();
            ViewBag.CanSetStatus = await CanSetStatusAsync("create");
            ViewBag.CanPublish   = await CanPublishMaterialAsync();
            await PopulateDropdownsAsync();
            return View(vm);
        }

        if (await _materials.TitleExistsAsync(vm.Title))
        {
            ModelState.AddModelError(nameof(vm.Title), _t.T("mat.title_duplicate"));
            ViewBag.CurrentUserFullName = CurrentUserFullName();
            ViewBag.CanSetStatus = await CanSetStatusAsync("create");
            ViewBag.CanPublish   = await CanPublishMaterialAsync();
            await PopulateDropdownsAsync();
            return View(vm);
        }

        // Enforce status permission: se l'utente non può cambiare stato, forza "under_review"
        if (!await CanSetStatusAsync("create"))
            vm.Status = "under_review";

        // Enforce publish permission: se l'utente non può pubblicare, azzera i campi publish
        bool canPublish = await CanPublishMaterialAsync();
        if (!canPublish)
        {
            vm.IsPublishable = false;
            vm.ExternalProtocolCode = null;
            vm.PlatformId = null;
            vm.ExternalLink = null;
        }

        // Resolve folder and assign protocol when status = verified
        int? resolvedFolderId = null;
        int? assignedProtocol = null;
        if (vm.Status == "verified")
        {
            if (vm.FolderId.HasValue)
                resolvedFolderId = vm.FolderId;
            else if (!string.IsNullOrWhiteSpace(vm.FolderName))
                resolvedFolderId = await _materials.GetOrCreateFolderAsync(vm.FolderName);
            assignedProtocol = await _materials.GetNextProtocolNumberAsync();
        }

        var matId = await _materials.CreateAsync(vm.Title, vm.AuthorName, vm.OwnerId, vm.Language, vm.DocumentTypeId, vm.Status, resolvedFolderId, vm.AreaId, vm.CatalogationDate, assignedProtocol, vm.PageCount, vm.IsPublishable, vm.ExternalProtocolCode, vm.PlatformId, vm.ExternalLink);

        if (vm.File != null && vm.File.Length > 0)
        {
            try
            {
                await SaveVersionAsync(matId, vm.File, vm.Notes, vm.ConvertToPdf);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveVersionAsync fallita per materialId={MatId} file={File}", matId, vm.File.FileName);
                TempData["Warning"] = _t.T("mat.msg_created_no_file") + " " + ex.Message;
                return RedirectToAction(nameof(Details), new { id = matId });
            }
        }

        _audit.Log("material.create", $"material#{matId} \"{vm.Title}\"");
        FireMaterialNotification(vm.Title, "created");
        TempData["Success"] = string.Format(_t.T("mat.msg_created"), vm.Title);
        return RedirectToAction(nameof(Details), new { id = matId });
    }

    // ── Edit ──────────────────────────────────────────────────────────────

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!await CanEditMaterialAsync()) return Forbid();
        var material = await _materials.GetByIdAsync(id);
        if (material == null) return NotFound();
        await PopulateDropdownsAsync();
        var vm = new MaterialFormViewModel
        {
            Id               = material.Id,
            Title            = material.Title,
            AuthorName       = material.AuthorName,
            OwnerId          = material.OwnerId,
            Language         = material.Language,
            DocumentTypeId   = material.DocumentTypeId,
            Status           = material.Status,
            FolderId              = material.FolderId,
            FolderName            = material.FolderName,
            AreaId                = material.AreaId,
            CatalogationDate      = material.CatalogationDate ?? DateTime.Today,
            IsPublishable         = material.IsPublishable,
            ExternalProtocolCode  = material.ExternalProtocolCode,
            PlatformId            = material.PlatformId,
            ExternalLink          = material.ExternalLink
        };
        ViewBag.Material     = material;
        ViewBag.CanSetStatus = await CanSetStatusAsync("edit");
        ViewBag.CanPublish   = await CanPublishMaterialAsync();
        return View(vm);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MaterialFormViewModel vm)
    {
        if (!await CanEditMaterialAsync()) return Forbid();
        // Try to extract author from document metadata if field is empty
        if (string.IsNullOrWhiteSpace(vm.AuthorName) && vm.File != null)
        {
            vm.AuthorName = await TryExtractAuthorAsync(vm.File);
            if (!string.IsNullOrWhiteSpace(vm.AuthorName))
                ModelState.Remove(nameof(vm.AuthorName));
        }

        // Try to extract page count from new PDF file if JS didn't provide it
        if (!vm.PageCount.HasValue && vm.File != null &&
            Path.GetExtension(vm.File.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            vm.PageCount = await TryExtractPageCountFromPdfAsync(vm.File);
        }

        if (string.IsNullOrWhiteSpace(vm.AuthorName))
            ModelState.AddModelError(nameof(vm.AuthorName), _t.T("mat.author_required"));

        TranslateDocTypeError(nameof(vm.DocumentTypeId), vm.DocumentTypeId);

        if (!ModelState.IsValid)
        {
            var mat = await _materials.GetByIdAsync(id);
            ViewBag.Material     = mat;
            ViewBag.CanSetStatus = await CanSetStatusAsync("edit");
            ViewBag.CanPublish   = await CanPublishMaterialAsync();
            await PopulateDropdownsAsync();
            return View(vm);
        }

        if (await _materials.TitleExistsAsync(vm.Title, id))
        {
            ModelState.AddModelError(nameof(vm.Title), _t.T("mat.title_duplicate"));
            var mat = await _materials.GetByIdAsync(id);
            ViewBag.Material     = mat;
            ViewBag.CanSetStatus = await CanSetStatusAsync("edit");
            ViewBag.CanPublish   = await CanPublishMaterialAsync();
            await PopulateDropdownsAsync();
            return View(vm);
        }

        // Enforce status permission: se l'utente non può cambiare stato,
        // promuove da "draft" a "under_review"; lascia invariato altrimenti
        if (!await CanSetStatusAsync("edit"))
        {
            var current = await _materials.GetByIdAsync(id);
            var currentStatus = current?.Status ?? "draft";
            vm.Status = currentStatus == "draft" ? "under_review" : currentStatus;
        }

        // Enforce publish permission: se l'utente non può pubblicare, preserva i valori attuali del DB
        bool canPublish = await CanPublishMaterialAsync();
        if (!canPublish)
        {
            var current = await _materials.GetByIdAsync(id);
            vm.IsPublishable        = current?.IsPublishable ?? false;
            vm.ExternalProtocolCode = current?.ExternalProtocolCode;
            vm.PlatformId           = current?.PlatformId;
            vm.ExternalLink         = current?.ExternalLink;
        }

        // Resolve folder and assign protocol if transitioning to verified without them
        int? resolvedFolderId = null;
        int? assignedProtocol = null;
        if (vm.Status == "verified")
        {
            var existing = await _materials.GetByIdAsync(id);
            if (vm.FolderId.HasValue)
                resolvedFolderId = vm.FolderId;
            else if (!string.IsNullOrWhiteSpace(vm.FolderName))
                resolvedFolderId = await _materials.GetOrCreateFolderAsync(vm.FolderName);

            if (existing?.ProtocolNumber == null)
                assignedProtocol = await _materials.GetNextProtocolNumberAsync();
        }

        await _materials.UpdateAsync(id, vm.Title, vm.AuthorName, vm.OwnerId, vm.Language, vm.DocumentTypeId, vm.Status, resolvedFolderId, vm.AreaId, vm.CatalogationDate, assignedProtocol, vm.PageCount, vm.IsPublishable, vm.ExternalProtocolCode, vm.PlatformId, vm.ExternalLink);

        if (vm.File != null && vm.File.Length > 0)
        {
            try
            {
                await SaveVersionAsync(id, vm.File, vm.Notes, vm.ConvertToPdf);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveVersionAsync fallita in Edit per materialId={Id} file={File}", id, vm.File.FileName);
                FireMaterialNotification(vm.Title, "updated");
                TempData["Warning"] = _t.T("mat.msg_updated_no_file") + " " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        _audit.Log("material.edit", $"material#{id} \"{vm.Title}\"");
        FireMaterialNotification(vm.Title, "updated");
        TempData["Success"] = _t.T("mat.msg_updated");
        return RedirectToAction(nameof(Index));
    }

    // ── Upload new version (from Details page) ────────────────────────────

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadVersion(int id, IFormFile file, string? notes)
    {
        if (!await CanEditMaterialAsync()) return Forbid();
        var material = await _materials.GetByIdAsync(id);
        if (material == null) return NotFound();
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = _t.T("mat.msg_select_file");
            return RedirectToAction(nameof(Details), new { id });
        }
        try
        {
            await SaveVersionAsync(id, file, notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveVersionAsync fallita in UploadVersion per materialId={Id} file={File}", id, file.FileName);
            TempData["Error"] = _t.T("mat.msg_file_save_error") + " " + ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
        TempData["Success"] = _t.T("mat.msg_version_uploaded");
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── Restore version ───────────────────────────────────────────────────

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int materialId, int versionId)
    {
        if (!await CanEditMaterialAsync()) return Forbid();
        var material = await _materials.GetByIdAsync(materialId);
        if (material == null) return NotFound();
        await _materials.RestoreVersionAsync(materialId, versionId);
        TempData["Success"] = _t.T("mat.msg_version_restored");
        return RedirectToAction(nameof(Details), new { id = materialId });
    }

    // ── Delete version ────────────────────────────────────────────────────

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVersion(int versionId, int materialId)
    {
        if (!await CanEditMaterialAsync()) return Forbid();
        var material = await _materials.GetByIdAsync(materialId);
        if (material == null) return NotFound();

        var version = await _materials.GetVersionByIdAsync(versionId);
        if (version == null || version.MaterialId != materialId) return NotFound();

        var count = await _materials.CountVersionsAsync(materialId);
        if (count <= 1)
        {
            TempData["Error"] = _t.T("mat.msg_version_last");
            return RedirectToAction(nameof(Details), new { id = materialId });
        }

        if (version.IsActive)
        {
            var versions = await _materials.GetVersionsAsync(materialId);
            var prev = versions.Where(v => v.Id != versionId).OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            if (prev != null)
                await _materials.RestoreVersionAsync(materialId, prev.Id);
        }

        var fullPath = Path.Combine(_env.WebRootPath, version.FilePath.TrimStart('/'));
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);

        await _materials.DeleteVersionAsync(versionId);
        TempData["Success"] = string.Format(_t.T("mat.msg_version_deleted"), version.VersionNumber);
        return RedirectToAction(nameof(Details), new { id = materialId });
    }

    // ── Bulk download (ZIP) ───────────────────────────────────────────────

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDownload(List<int> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            TempData["Error"] = _t.T("mat.msg_select_at_least_one");
            return RedirectToAction(nameof(Index));
        }

        var ms = new MemoryStream();
        var added = 0;
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var matId in ids)
            {
                var material = await _materials.GetByIdAsync(matId);
                if (material?.ActiveVersion == null) continue;
                var version = material.ActiveVersion;
                var fullPath = Path.Combine(_env.WebRootPath, version.FilePath.TrimStart('/'));
                if (!System.IO.File.Exists(fullPath)) continue;

                var entryName = $"{material.Title} - v{version.VersionNumber}{Path.GetExtension(version.FileName)}";
                entryName = string.Join("_", entryName.Split(Path.GetInvalidFileNameChars()));
                var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var fileStream = System.IO.File.OpenRead(fullPath);
                await fileStream.CopyToAsync(entryStream);
                added++;
            }
        }

        if (added == 0)
        {
            TempData["Error"] = _t.T("mat.msg_no_files_available");
            return RedirectToAction(nameof(Index));
        }

        ms.Position = 0;
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        return File(ms, "application/zip", $"materiali_{timestamp}.zip");
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

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await CanEditMaterialAsync()) return Forbid();
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

        var deletedTitle = material.Title;
        await _materials.DeleteAsync(id);
        _audit.Log("material.delete", $"material#{id} \"{deletedTitle}\"");
        FireMaterialNotification(deletedTitle, "deleted");
        TempData["Success"] = string.Format(_t.T("mat.msg_deleted"), deletedTitle);
        return RedirectToAction(nameof(Index));
    }

    // ── Lesson integration ────────────────────────────────────────────────

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkToLesson(int lessonId, int materialId)
    {
        if (!await CanEditMaterialAsync()) return Forbid();
        await _materials.LinkToLessonAsync(lessonId, materialId, CurrentUserId());
        TempData["Success"] = _t.T("mat.msg_linked_lesson");
        return RedirectToAction("Details", "Lesson", new { id = lessonId });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlinkFromLesson(int lessonId, int materialId)
    {
        if (!await CanEditMaterialAsync()) return Forbid();
        await _materials.UnlinkFromLessonAsync(lessonId, materialId);
        TempData["Success"] = _t.T("mat.msg_unlinked_lesson");
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
