using System.Data;
using BocconiLMS.Data;
using BocconiLMS.Models;
using Microsoft.Data.SqlClient;
using MySqlConnector;

namespace BocconiLMS.Services;

public class DataImportService
{
    private readonly DbHelper _db;
    private readonly ILogger<DataImportService> _log;

    public DataImportService(DbHelper db, ILogger<DataImportService> log)
    {
        _db  = db;
        _log = log;
    }

    // ── Connection helpers ────────────────────────────────────────────────

    public async Task<(bool ok, string? err, int tableCount)> TestConnectionAsync(string connStr)
    {
        try
        {
            var b = new SqlConnectionStringBuilder(connStr) { ConnectTimeout = 10 };
            await using var c = new SqlConnection(b.ConnectionString);
            await c.OpenAsync();

            await using var cmd = new SqlCommand(@"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'", c);
            var n = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return (true, null, n);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, 0);
        }
    }

    public static string Mask(string connStr)
    {
        try
        {
            var b = new SqlConnectionStringBuilder(connStr);
            var srv = string.IsNullOrWhiteSpace(b.DataSource) ? "?" : b.DataSource;
            var db  = string.IsNullOrWhiteSpace(b.InitialCatalog) ? "?" : b.InitialCatalog;
            var usr = string.IsNullOrWhiteSpace(b.UserID) ? "(integrated)" : b.UserID;
            return $"Server={srv} · DB={db} · User={usr}";
        }
        catch { return "(connection string non valida)"; }
    }

    // ── Schema discovery ──────────────────────────────────────────────────

    public async Task<List<SourceTableInfo>> ListTablesAsync(string connStr)
    {
        var list = new List<SourceTableInfo>();
        await using var c = new SqlConnection(connStr);
        await c.OpenAsync();

        await using var cmd = new SqlCommand(@"
            SELECT t.TABLE_SCHEMA, t.TABLE_NAME,
                   ISNULL(p.rows, 0) AS row_count
            FROM   INFORMATION_SCHEMA.TABLES t
            LEFT JOIN sys.tables st  ON st.name   = t.TABLE_NAME
                                    AND SCHEMA_NAME(st.schema_id) = t.TABLE_SCHEMA
            LEFT JOIN sys.partitions p ON p.object_id = st.object_id
                                    AND p.index_id IN (0,1)
            WHERE  t.TABLE_TYPE = 'BASE TABLE'
            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME", c);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new SourceTableInfo
            {
                Schema   = r.GetString(0),
                Name     = r.GetString(1),
                RowCount = r.IsDBNull(2) ? 0 : Convert.ToInt64(r.GetValue(2))
            });
        }
        return list;
    }

    public async Task<List<SourceColumnInfo>> GetColumnsAsync(
        string connStr, string schema, string table)
    {
        var list = new List<SourceColumnInfo>();
        await using var c = new SqlConnection(connStr);
        await c.OpenAsync();

        await using var cmd = new SqlCommand(@"
            SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
            FROM   INFORMATION_SCHEMA.COLUMNS
            WHERE  TABLE_SCHEMA = @s AND TABLE_NAME = @t
            ORDER  BY ORDINAL_POSITION", c);
        cmd.Parameters.AddWithValue("@s", schema);
        cmd.Parameters.AddWithValue("@t", table);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new SourceColumnInfo
            {
                Name       = r.GetString(0),
                DataType   = r.GetString(1),
                IsNullable = string.Equals(r.GetString(2), "YES", StringComparison.OrdinalIgnoreCase)
            });
        }
        return list;
    }

    public async Task<DataTable> PreviewRowsAsync(
        string connStr, string schema, string table, int limit = 50)
    {
        var dt = new DataTable();
        await using var c = new SqlConnection(connStr);
        await c.OpenAsync();
        // Quote identifiers safely; parameters cannot be used in identifiers.
        var qSchema = "[" + schema.Replace("]", "]]") + "]";
        var qTable  = "[" + table .Replace("]", "]]") + "]";
        await using var cmd = new SqlCommand(
            $"SELECT TOP ({limit}) * FROM {qSchema}.{qTable}", c);
        await using var r = await cmd.ExecuteReaderAsync();
        dt.Load(r);
        return dt;
    }

    // ── Identifier safety ─────────────────────────────────────────────────

    private static bool IsValidIdent(string s) =>
        !string.IsNullOrWhiteSpace(s) &&
        s.Length <= 128 &&
        s.All(ch => char.IsLetterOrDigit(ch) || ch == '_');

    // ── Import execution ──────────────────────────────────────────────────

    public async Task<ImportResultVm> ExecuteImportAsync(
        string connStr, ImportMappingVm map, bool dryRun)
    {
        if (!IsValidIdent(map.SourceSchema) || !IsValidIdent(map.SourceTable))
            throw new InvalidOperationException("Schema/Table sorgente non valido.");

        return map.Target switch
        {
            ImportTarget.MaterialFolders =>
                await ImportFoldersAsync(connStr, map, dryRun),
            ImportTarget.Materials =>
                await ImportMaterialsAsync(connStr, map, dryRun),
            _ => throw new NotSupportedException()
        };
    }

    // ── material_folders import ───────────────────────────────────────────

    private async Task<ImportResultVm> ImportFoldersAsync(
        string connStr, ImportMappingVm map, bool dryRun)
    {
        var nameMap = map.Mappings.FirstOrDefault(m => m.TargetField == "name")
            ?? throw new InvalidOperationException("Mapping per 'name' obbligatorio.");

        var srcRows = await ReadSourceAsync(connStr, map);
        var result  = new ImportResultVm
        {
            DryRun     = dryRun,
            SourceRows = srcRows.Rows.Count,
            Target     = ImportTarget.MaterialFolders
        };

        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            // Pre-load existing folder names
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = new MySqlCommand(
                "SELECT name FROM material_folders", conn, tx))
            await using (var r = await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync()) existing.Add(r.GetString(0));

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < srcRows.Rows.Count; i++)
            {
                var row = srcRows.Rows[i];
                string? name;
                try
                {
                    name = ApplyTransform(row, nameMap)?.ToString()?.Trim();
                }
                catch (Exception ex)
                {
                    AddError(result, i, $"transform: {ex.Message}", row);
                    continue;
                }

                if (string.IsNullOrEmpty(name)) { result.Skipped++; continue; }
                if (!seen.Add(name))            { result.Skipped++; continue; } // duplicate inside batch

                if (existing.Contains(name))
                {
                    if (map.Conflict == ConflictPolicy.Skip ||
                        map.Conflict == ConflictPolicy.DryRunOnly ||
                        map.Conflict == ConflictPolicy.Update) // folders have no extra fields
                    {
                        result.Skipped++;
                        AddPreview(result, new() { ["name"] = name, ["__action"] = "skip-exists" });
                        continue;
                    }
                }
                else
                {
                    if (!dryRun)
                    {
                        await using var ins = new MySqlCommand(
                            "INSERT INTO material_folders (name) VALUES (@n)", conn, tx);
                        ins.Parameters.AddWithValue("@n", name);
                        await ins.ExecuteNonQueryAsync();
                    }
                    result.Inserted++;
                    AddPreview(result, new() { ["name"] = name, ["__action"] = "insert" });
                }
            }

            if (dryRun) await tx.RollbackAsync();
            else        await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return result;
    }

    // ── materials import ──────────────────────────────────────────────────

    private async Task<ImportResultVm> ImportMaterialsAsync(
        string connStr, ImportMappingVm map, bool dryRun)
    {
        var titleMap = map.Mappings.FirstOrDefault(m => m.TargetField == "title")
            ?? throw new InvalidOperationException("Mapping per 'title' obbligatorio.");

        var srcRows = await ReadSourceAsync(connStr, map);
        var result  = new ImportResultVm
        {
            DryRun     = dryRun,
            SourceRows = srcRows.Rows.Count,
            Target     = ImportTarget.Materials
        };

        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            // Pre-load lookups
            var folders = await LoadLookupAsync(conn, tx, "material_folders", "name", "id");
            var dtypes  = await LoadLookupAsync(conn, tx, "document_types",  "name", "id");
            var areas   = await LoadLookupAsync(conn, tx, "areas",           "name", "id");
            var owners  = await LoadLookupAsync(conn, tx, "users",           "email","id");

            var existingTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = new MySqlCommand(
                "SELECT title FROM materials", conn, tx))
            await using (var r = await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync()) existingTitles.Add(r.GetString(0));

            for (int i = 0; i < srcRows.Rows.Count; i++)
            {
                var row = srcRows.Rows[i];
                var values = new Dictionary<string, object?>();

                try
                {
                    foreach (var m in map.Mappings)
                    {
                        if (string.IsNullOrEmpty(m.SourceField) &&
                            m.Transform != ImportTransform.AutoCreateFolderByName)
                            continue;

                        var raw = ApplyTransform(row, m);
                        var resolved = ResolveLookup(m, raw, folders, dtypes, areas, owners,
                                                    conn, tx, dryRun);
                        values[m.TargetField] = resolved;
                    }
                }
                catch (Exception ex)
                {
                    AddError(result, i, $"map: {ex.Message}", row);
                    continue;
                }

                var title = values.TryGetValue("title", out var t) ? t?.ToString()?.Trim() : null;
                if (string.IsNullOrEmpty(title))
                {
                    AddError(result, i, "title vuoto", row);
                    continue;
                }
                values["title"] = title;

                if (existingTitles.Contains(title))
                {
                    if (map.Conflict == ConflictPolicy.Skip ||
                        map.Conflict == ConflictPolicy.DryRunOnly)
                    {
                        result.Skipped++;
                        AddPreview(result, MergePreview(values, "skip-exists"));
                        continue;
                    }
                    if (map.Conflict == ConflictPolicy.Update)
                    {
                        if (!dryRun) await UpdateMaterialAsync(conn, tx, title, values);
                        result.Updated++;
                        AddPreview(result, MergePreview(values, "update"));
                        continue;
                    }
                }
                else
                {
                    if (!dryRun) await InsertMaterialAsync(conn, tx, values);
                    existingTitles.Add(title);
                    result.Inserted++;
                    AddPreview(result, MergePreview(values, "insert"));
                }
            }

            if (dryRun) await tx.RollbackAsync();
            else        await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<DataTable> ReadSourceAsync(string connStr, ImportMappingVm map)
    {
        var dt = new DataTable();
        await using var c = new SqlConnection(connStr);
        await c.OpenAsync();
        var qS = "[" + map.SourceSchema.Replace("]", "]]") + "]";
        var qT = "[" + map.SourceTable .Replace("]", "]]") + "]";
        await using var cmd = new SqlCommand(
            $"SELECT TOP (500000) * FROM {qS}.{qT}", c);
        cmd.CommandTimeout = 120;
        await using var r = await cmd.ExecuteReaderAsync();
        dt.Load(r);
        return dt;
    }

    private static async Task<Dictionary<string, int>> LoadLookupAsync(
        MySqlConnection conn, System.Data.Common.DbTransaction tx,
        string table, string nameCol, string idCol)
    {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new MySqlCommand(
            $"SELECT {nameCol}, {idCol} FROM {table}", conn, (MySqlTransaction)tx);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            if (r.IsDBNull(0)) continue;
            d[r.GetString(0)] = r.GetInt32(1);
        }
        return d;
    }

    private static object? ApplyTransform(DataRow row, ColumnMapping m)
    {
        object? GetCol(string? col)
        {
            if (string.IsNullOrEmpty(col) || !row.Table.Columns.Contains(col)) return null;
            var v = row[col];
            return v == DBNull.Value ? null : v;
        }

        var v1 = GetCol(m.SourceField);

        switch (m.Transform)
        {
            case ImportTransform.None:
                return v1;
            case ImportTransform.Concat:
                var v2 = GetCol(m.SourceField2);
                var sep = m.TransformParam ?? " ";
                return string.Join(sep, new[] { v1?.ToString(), v2?.ToString() }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
            case ImportTransform.Lower:
                return v1?.ToString()?.ToLowerInvariant();
            case ImportTransform.Upper:
                return v1?.ToString()?.ToUpperInvariant();
            case ImportTransform.ParseDate:
                if (v1 == null) return null;
                if (v1 is DateTime dt) return dt;
                var fmt = m.TransformParam;
                if (!string.IsNullOrEmpty(fmt))
                    return DateTime.ParseExact(v1.ToString()!, fmt,
                        System.Globalization.CultureInfo.InvariantCulture);
                return DateTime.Parse(v1.ToString()!,
                    System.Globalization.CultureInfo.InvariantCulture);
            case ImportTransform.BoolFromInt:
                if (v1 == null) return null;
                if (v1 is bool b) return b;
                return Convert.ToInt32(v1) != 0;
            case ImportTransform.LookupAreaByName:
            case ImportTransform.LookupDocTypeByName:
            case ImportTransform.LookupOwnerByEmail:
            case ImportTransform.LookupFolderByName:
            case ImportTransform.AutoCreateFolderByName:
                return v1; // resolved later in ResolveLookup
            default:
                return v1;
        }
    }

    private static object? ResolveLookup(
        ColumnMapping m, object? raw,
        Dictionary<string, int> folders,
        Dictionary<string, int> dtypes,
        Dictionary<string, int> areas,
        Dictionary<string, int> owners,
        MySqlConnection conn, System.Data.Common.DbTransaction tx,
        bool dryRun)
    {
        if (raw == null) return null;
        var key = raw.ToString()?.Trim();
        if (string.IsNullOrEmpty(key)) return null;

        switch (m.Transform)
        {
            case ImportTransform.LookupAreaByName:
                return areas.TryGetValue(key, out var aid)  ? aid : (int?)null;
            case ImportTransform.LookupDocTypeByName:
                return dtypes.TryGetValue(key, out var did) ? did : (int?)null;
            case ImportTransform.LookupOwnerByEmail:
                return owners.TryGetValue(key, out var oid) ? oid : (int?)null;
            case ImportTransform.LookupFolderByName:
                return folders.TryGetValue(key, out var fid) ? fid : (int?)null;
            case ImportTransform.AutoCreateFolderByName:
                if (folders.TryGetValue(key, out var ex)) return ex;
                if (dryRun) return -1;
                using (var ins = new MySqlCommand(
                    "INSERT INTO material_folders (name) VALUES (@n); SELECT LAST_INSERT_ID();",
                    conn, (MySqlTransaction)tx))
                {
                    ins.Parameters.AddWithValue("@n", key);
                    var newId = Convert.ToInt32(ins.ExecuteScalar());
                    folders[key] = newId;
                    return newId;
                }
            default:
                return raw;
        }
    }

    private static readonly string[] AllowedMaterialColumns = new[]
    {
        "title","author_name","owner_id","language","document_type_id",
        "status","protocol_number","folder_id","folder","area_id",
        "catalogation_date"
    };

    private static async Task InsertMaterialAsync(
        MySqlConnection conn, System.Data.Common.DbTransaction tx,
        Dictionary<string, object?> values)
    {
        var cols = values.Keys.Where(k => AllowedMaterialColumns.Contains(k)).ToList();
        if (cols.Count == 0) throw new InvalidOperationException("nessun campo valido");

        var sql = $"INSERT INTO materials ({string.Join(",", cols)}) " +
                  $"VALUES ({string.Join(",", cols.Select(c => "@" + c))})";
        await using var cmd = new MySqlCommand(sql, conn, (MySqlTransaction)tx);
        foreach (var c in cols)
            cmd.Parameters.AddWithValue("@" + c, values[c] ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task UpdateMaterialAsync(
        MySqlConnection conn, System.Data.Common.DbTransaction tx,
        string title, Dictionary<string, object?> values)
    {
        var cols = values.Keys
            .Where(k => AllowedMaterialColumns.Contains(k) && k != "title")
            .ToList();
        if (cols.Count == 0) return;

        var set = string.Join(",", cols.Select(c => $"{c}=@{c}"));
        var sql = $"UPDATE materials SET {set} WHERE title=@__title";
        await using var cmd = new MySqlCommand(sql, conn, (MySqlTransaction)tx);
        foreach (var c in cols)
            cmd.Parameters.AddWithValue("@" + c, values[c] ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@__title", title);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddError(ImportResultVm r, int i, string reason, DataRow row)
    {
        r.ErrorsCount++;
        if (r.Errors.Count < 100)
        {
            var snippet = string.Join(" | ", row.Table.Columns
                .Cast<DataColumn>()
                .Take(4)
                .Select(c => $"{c.ColumnName}={row[c]}"));
            r.Errors.Add(new ImportRowError { RowIndex = i, Reason = reason, Snippet = snippet });
        }
    }

    private static void AddPreview(ImportResultVm r, Dictionary<string, object?> v)
    {
        if (r.PreviewRows.Count < 20) r.PreviewRows.Add(v);
    }

    private static Dictionary<string, object?> MergePreview(
        Dictionary<string, object?> values, string action)
    {
        var d = new Dictionary<string, object?>(values);
        d["__action"] = action;
        return d;
    }
}
