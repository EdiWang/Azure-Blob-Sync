namespace Edi.AzureBlobSync;

internal class FileSyncInfoComparer : IEqualityComparer<FileSyncInfo>
{
    public bool Equals(FileSyncInfo x, FileSyncInfo y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        return string.Equals(x.FileName, y.FileName, StringComparison.OrdinalIgnoreCase) &&
               x.Length == y.Length &&
               string.Equals(x.ContentMD5, y.ContentMD5, StringComparison.Ordinal);
    }

    public int GetHashCode(FileSyncInfo obj)
    {
        return HashCode.Combine(
            obj.FileName?.ToLowerInvariant(),
            obj.Length,
            obj.ContentMD5);
    }
}