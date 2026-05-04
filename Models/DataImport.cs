using System.ComponentModel.DataAnnotations;

namespace BocconiLMS.Models;

public class SqlSourceConnectionVm
{
    [Required(ErrorMessage = "La connection string è obbligatoria.")]
    public string ConnectionString { get; set; } = "";
    public string? MaskedDisplay   { get; set; }
    public int    TableCount       { get; set; }
    public string? TestError       { get; set; }
}

public class SourceTableInfo
{
    public string Schema   { get; set; } = "";
    public string Name     { get; set; } = "";
    public long   RowCount { get; set; }
    public string FullName => $"{Schema}.{Name}";
}

public class SourceColumnInfo
{
    public string Name       { get; set; } = "";
    public string DataType   { get; set; } = "";
    public bool   IsNullable { get; set; }
}

public enum ImportTransform
{
    None,
    Concat,
    Lower,
    Upper,
    ParseDate,
    BoolFromInt,
    LookupAreaByName,
    LookupDocTypeByName,
    LookupOwnerByEmail,
    LookupFolderByName,
    AutoCreateFolderByName
}

public class ColumnMapping
{
    public string TargetField   { get; set; } = "";
    public string? SourceField  { get; set; }
    public string? SourceField2 { get; set; }
    public ImportTransform Transform { get; set; } = ImportTransform.None;
    public string? TransformParam   { get; set; }
}

public enum ConflictPolicy { Skip, Update, DryRunOnly }

public enum ImportTarget { Materials, MaterialFolders }

public class ImportMappingVm
{
    public string SourceSchema { get; set; } = "";
    public string SourceTable  { get; set; } = "";
    public ImportTarget Target { get; set; } = ImportTarget.Materials;
    public List<ColumnMapping> Mappings { get; set; } = new();
    public ConflictPolicy Conflict { get; set; } = ConflictPolicy.DryRunOnly;
    public List<SourceColumnInfo> AvailableSourceColumns { get; set; } = new();
}

public class ImportRowError
{
    public int    RowIndex { get; set; }
    public string Reason   { get; set; } = "";
    public string? Snippet { get; set; }
}

public class ImportResultVm
{
    public bool   DryRun       { get; set; }
    public int    SourceRows   { get; set; }
    public int    Inserted     { get; set; }
    public int    Updated      { get; set; }
    public int    Skipped      { get; set; }
    public int    ErrorsCount  { get; set; }
    public List<ImportRowError> Errors { get; set; } = new();
    public List<Dictionary<string, object?>> PreviewRows { get; set; } = new();
    public ImportTarget Target { get; set; }
}
