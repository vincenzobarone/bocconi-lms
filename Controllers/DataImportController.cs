using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BocconiLMS.Models;
using BocconiLMS.Services;

namespace BocconiLMS.Controllers;

[Authorize(Roles = "Admin")]
public class DataImportController : Controller
{
    private const string SessConn = "DI_ConnStr";

    private readonly DataImportService               _svc;
    private readonly IAuditLogger                    _audit;
    private readonly ILogger<DataImportController>   _log;

    public DataImportController(
        DataImportService svc,
        IAuditLogger audit,
        ILogger<DataImportController> log)
    {
        _svc   = svc;
        _audit = audit;
        _log   = log;
    }

    // ── Step 1 · Connessione ──────────────────────────────────────────────

    [HttpGet]
    public IActionResult Connect() => View(new SqlSourceConnectionVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Connect(SqlSourceConnectionVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var (ok, err, n) = await _svc.TestConnectionAsync(vm.ConnectionString);
        if (!ok)
        {
            vm.TestError = err;
            return View(vm);
        }

        HttpContext.Session.SetString(SessConn, vm.ConnectionString);

        var mask = DataImportService.Mask(vm.ConnectionString);
        _audit.Log("DataImport.Connect",
            target:  mask,
            outcome: $"ok · {n} tabelle",
            user:    User.Identity?.Name,
            ip:      HttpContext.Connection.RemoteIpAddress?.ToString());

        return RedirectToAction(nameof(Tables));
    }

    // ── Step 2 · Scelta tabella ───────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Tables()
    {
        var cs = HttpContext.Session.GetString(SessConn);
        if (string.IsNullOrEmpty(cs)) return RedirectToAction(nameof(Connect));

        try
        {
            var tables = await _svc.ListTablesAsync(cs);
            ViewBag.Masked = DataImportService.Mask(cs);
            return View(tables);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Errore lettura schema: {ex.Message}";
            return RedirectToAction(nameof(Connect));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectTable(
        string schema, string table, ImportTarget target)
    {
        var cs = HttpContext.Session.GetString(SessConn);
        if (string.IsNullOrEmpty(cs)) return RedirectToAction(nameof(Connect));

        if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(table))
        {
            TempData["Error"] = "Seleziona una tabella prima di procedere.";
            return RedirectToAction(nameof(Tables));
        }

        List<SourceColumnInfo> cols;
        try { cols = await _svc.GetColumnsAsync(cs, schema, table); }
        catch (Exception ex)
        {
            TempData["Error"] = $"Errore lettura colonne: {ex.Message}";
            return RedirectToAction(nameof(Tables));
        }

        var vm = new ImportMappingVm
        {
            SourceSchema           = schema,
            SourceTable            = table,
            Target                 = target,
            AvailableSourceColumns = cols,
            Conflict               = ConflictPolicy.Skip,
            Mappings               = BuildDefaultMappings(target, cols)
        };
        ViewBag.Masked = DataImportService.Mask(cs);
        return View("Map", vm);
    }

    // ── Step 3 · Mapping + esecuzione ────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Execute(ImportMappingVm vm, bool dryRun)
    {
        var cs = HttpContext.Session.GetString(SessConn);
        if (string.IsNullOrEmpty(cs)) return RedirectToAction(nameof(Connect));

        // Reload source columns for re-render (always needed)
        try
        {
            vm.AvailableSourceColumns = await _svc.GetColumnsAsync(
                cs, vm.SourceSchema, vm.SourceTable);
        }
        catch { vm.AvailableSourceColumns = []; }

        ImportResultVm result;
        try
        {
            result = await _svc.ExecuteImportAsync(cs, vm, dryRun);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "DataImport execute failed ({Schema}.{Table})",
                vm.SourceSchema, vm.SourceTable);
            TempData["Error"] = $"Errore: {ex.Message}";
            ViewBag.Masked = DataImportService.Mask(cs);
            return View("Map", vm);
        }

        _audit.Log(dryRun ? "DataImport.DryRun" : "DataImport.Execute",
            target:  $"{vm.Target} · {vm.SourceSchema}.{vm.SourceTable}",
            outcome: $"ins={result.Inserted} upd={result.Updated} skip={result.Skipped} err={result.ErrorsCount}",
            user:    User.Identity?.Name,
            ip:      HttpContext.Connection.RemoteIpAddress?.ToString());

        if (dryRun)
        {
            ViewBag.DryRunResult = result;
            ViewBag.Masked       = DataImportService.Mask(cs);
            return View("Map", vm);
        }

        return View("Result", result);
    }

    // ── Annulla ───────────────────────────────────────────────────────────

    public IActionResult Cancel()
    {
        HttpContext.Session.Remove(SessConn);
        return RedirectToAction("Database", "Admin");
    }

    // ── Utilità ───────────────────────────────────────────────────────────

    private static readonly (string Field, string Label, bool Required, ImportTransform DefaultTransform)[] MaterialFields =
    [
        ("title",                  "Titolo",                     true,  ImportTransform.None),
        ("author_name",            "Autore",                    false,  ImportTransform.None),
        ("language",               "Lingua",                    false,  ImportTransform.None),
        ("status",                 "Stato",                     false,  ImportTransform.None),
        ("protocol_number",        "N. protocollo",             false,  ImportTransform.None),
        ("catalogation_date",      "Data catalogazione",        false,  ImportTransform.ParseDate),
        ("document_type_id",       "Tipo documento",            false,  ImportTransform.LookupDocTypeByName),
        ("owner_id",               "Responsabile (email→ID)",   false,  ImportTransform.LookupOwnerByEmail),
        ("area_id",                "Area",                      false,  ImportTransform.LookupAreaByName),
        ("folder_id",              "Cartella",                  false,  ImportTransform.LookupFolderByName),
        ("page_count",             "N. pagine",                 false,  ImportTransform.None),
        ("is_publishable",         "Pubblicabile (0/1)",        false,  ImportTransform.BoolFromInt),
        ("external_protocol_code", "Cod. protocollo esterno",   false,  ImportTransform.None),
        ("external_link",          "Link esterno",              false,  ImportTransform.None),
    ];

    private static readonly (string Field, string Label, bool Required, ImportTransform DefaultTransform)[] FolderFields =
    [
        ("name", "Nome cartella", true, ImportTransform.None),
    ];

    private static List<ColumnMapping> BuildDefaultMappings(
        ImportTarget target, List<SourceColumnInfo> cols)
    {
        var fields = target == ImportTarget.MaterialFolders
            ? FolderFields
            : MaterialFields;

        return fields.Select(f => new ColumnMapping
        {
            TargetField = f.Field,
            SourceField = cols.FirstOrDefault(c =>
                string.Equals(c.Name, f.Field, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Name, f.Field.Replace("_id", ""), StringComparison.OrdinalIgnoreCase))?.Name,
            Transform = f.DefaultTransform,
        }).ToList();
    }
}
