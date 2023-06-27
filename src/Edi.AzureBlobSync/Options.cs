using CommandLine;

namespace Edi.AzureBlobSync;

internal class Options
{
    [Option(longName: "connection", HelpText = "Storage Account Connection String")]
    public string ConnectionString { get; set; }

    [Option(longName: "container", HelpText = "Blob Container Name")]
    public string Container { get; set; }

    [Option(longName: "path", HelpText = "Local Folder Path")]
    public string Path { get; set; }

    [Option(longName: "threads", Default = 10, Required = false, HelpText = "Download threads")]
    public int Threads { get; set; }

    [Option(longName: "silence", Default = false, Required = false, HelpText = "Silence mode")]
    public bool Silence { get; set; }

    [Option(longName: "comparehash", Default = (bool)true, Required = false, HelpText = "Compare file hash")]
    public bool? CompareHash { get; set; }
}