namespace BocconiLMS.Models;

public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Cartella base degli upload, relativa a wwwroot.
    /// Corrisponde al prefisso salvato nei file_path di material_versions.
    /// Default: "uploads". Override via appsettings.json o env var Storage__UploadRoot.
    /// </summary>
    public string UploadRoot { get; set; } = "uploads";
}
