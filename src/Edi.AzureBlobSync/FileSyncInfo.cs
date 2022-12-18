namespace Edi.AzureBlobSync;

internal class FileSyncInfo
{
    public string FileName { get; set; }

    public long? Length { get; set; }
    public string ContentMD5 { get; set; }
    public bool IsArchive { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is FileSyncInfo si)
        {
            return 
                si.FileName == FileName && 
                si.Length == Length && 
                si.ContentMD5 == ContentMD5;
        }
        return false;
    }

    public override int GetHashCode()
    {
        // Seems wrong implementation of GetHashCode()
        // But why I wrote this method in the first place?
        // Emmmmmm... who cares anyway
        return $"{FileName}{Length}".Length;
    }
}