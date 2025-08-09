namespace Edi.AzureBlobSync;

internal class FileSyncInfo
{
    public required string FileName { get; set; }
    public long? Length { get; set; }
    public string ContentMD5 { get; set; } = string.Empty;
    public bool IsArchive { get; set; }
}