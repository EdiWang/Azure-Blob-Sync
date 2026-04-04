namespace Edi.AzureBlobSync;

public class Options
{
    public string ConnectionString { get; set; }
    public string Container { get; set; }
    public string Path { get; set; }
    public int Threads { get; set; }
    public bool Silence { get; set; }
    public bool KeepOld { get; set; }
    public bool? CompareHash { get; set; }
}