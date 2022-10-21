using System.Reflection;
using Azure.Storage.Blobs;
using CommandLine;
using Spectre.Console;

namespace Edi.AzureBlobSync;

internal class Options
{
    [Option(longName: "connection", Required = true, HelpText = "Storage Account Connection String")]
    public string ConnectionString { get; set; }

    [Option(longName: "container", Required = true, HelpText = "Blob Container Name")]
    public string Container { get; set; }

    [Option(longName: "path", Default = "C:\\AzureBlobSyncTemp", Required = true, HelpText = "Local Folder Path")]
    public string Path { get; set; }

    [Option(longName: "threads", Default = 10, Required = false, HelpText = "Download threads")]
    public int Threads { get; set; }

    [Option(longName: "silence", Default = false, Required = false, HelpText = "Silence mode")]
    public bool Silence { get; set; }
}

internal class FileSyncInfo
{
    public string FileName { get; set; }

    public long? Length { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is FileSyncInfo si)
        {
            return si.FileName == FileName && si.Length == Length;
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

class Program
{
    public static Options Options { get; set; }

    public static BlobContainerClient BlobContainer { get; set; }

    public static async Task Main(string[] args)
    {
        var parserResult = Parser.Default.ParseArguments<Options>(args);
        if (parserResult.Tag == ParserResultType.Parsed)
        {
            Options = ((Parsed<Options>)parserResult).Value;
            WriteParameterTable();

            if (!Options.Silence)
            {
                if (!AnsiConsole.Confirm("Good to go?")) return;
            }

            try
            {
                // 1. Get Azure Blob Files
                BlobContainer = GetBlobContainer();

                var cloudFiles = new List<FileSyncInfo>();
                await AnsiConsole.Status()
                    .StartAsync("Finding files on Azure Storage...", async _ =>
                    {
                        await foreach (var blobItem in BlobContainer.GetBlobsAsync())
                        {
                            var fsi = new FileSyncInfo
                            {
                                FileName = blobItem.Name,
                                Length = blobItem.Properties.ContentLength
                            };
                            cloudFiles.Add(fsi);
                        }
                    });

                AnsiConsole.Write(new Markup($"[green]{cloudFiles.Count}[/] cloud file{(cloudFiles.Count > 0 ? "s" : string.Empty)} found.\n"));

                // 2. Get Local Files
                if (!Directory.Exists(Options.Path))
                {
                    Directory.CreateDirectory(Options.Path);
                }

                var localFilePaths = Directory.GetFiles(Options.Path);
                var localFiles = localFilePaths.Select(filePath => new FileInfo(filePath))
                    .Select(fi => new FileSyncInfo
                    {
                        FileName = fi.Name,
                        Length = fi.Length,
                    })
                    .ToList();

                AnsiConsole.Write(new Markup($"[green]{localFiles.Count}[/] local file(s) found.\n"));

                // 3. Compare Files
                AnsiConsole.WriteLine("Comparing file meta data...");

                // Files in cloud but not in local
                var excepts = cloudFiles.Except(localFiles).ToList();
                if (excepts.Any())
                {
                    if (Options.Silence || AnsiConsole.Confirm($"[green]{excepts.Count}[/] new file(s) to download. Continue?"))
                    {
                        await DownloadAll(excepts);
                    }
                    else
                    {
                        excepts.Clear();
                    }
                }
                else
                {
                    AnsiConsole.WriteLine("No new files need to be downloaded.");
                }

                // 5. Ask Delete Old Files
                var localExcepts = localFiles.Except(cloudFiles).ToList();
                var deleteCount = 0;
                if (localExcepts.Any())
                {
                    if (!Options.Silence)
                    {
                        AnsiConsole.Write(new Markup($"[yellow]{localExcepts.Count}[/] redundancy file(s) exists in local but not on cloud, [blue][[V]][/] to view file list, [blue][[ENTER]][/] to continue.\n"));

                        if (Console.ReadKey().Key == ConsoleKey.V)
                        {
                            Console.WriteLine();
                            var localExceptsTable = new Table();

                            localExceptsTable.AddColumn("File Name");
                            localExceptsTable.AddColumn("Length (bytes)");

                            foreach (var f in localExcepts)
                            {
                                localExceptsTable.AddRow(f.FileName, f.Length.ToString());
                            }

                            AnsiConsole.Write(localExceptsTable);
                        }
                    }

                    if (Options.Silence || AnsiConsole.Confirm("[yellow]Do you want to delete these files?[/]"))
                    {
                        AnsiConsole.WriteLine("Deleting local redundancy files...");
                        foreach (var fi in localExcepts)
                        {
                            File.Delete(Path.Combine(Options.Path, fi.FileName));
                            deleteCount++;
                        }
                    }
                }

                AnsiConsole.WriteLine("----------------------------------------------------");
                AnsiConsole.Write(new Markup($"Local Files Up to Date. [green]{excepts.Count}[/] new file(s) downloaded, [yellow]{deleteCount}[/] file(s) deleted."));
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(e);
            }
        }
        else
        {
            AnsiConsole.Write(new Markup("[red]ERROR: Failed to parse console parameters.[/]"));
        }

        Console.ReadKey();
    }

    private static void WriteParameterTable()
    {
        var appVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        var table = new Table
        {
            Title = new TableTitle($"Edi.AzureBlobSync {appVersion}")
        };

        table.AddColumn("Parameter");
        table.AddColumn("Value");
        table.AddRow(new Markup("[blue]Container Name[/]"), new Text(Options.Container));
        table.AddRow(new Markup("[blue]Download Threads[/]"), new Text(Options.Threads.ToString()));
        table.AddRow(new Markup("[blue]Local Path[/]"), new Text(Options.Path));
        AnsiConsole.Write(table);
    }

    private static async Task DownloadAll(List<FileSyncInfo> excepts)
    {
        using var semaphore = new SemaphoreSlim(Options.Threads);
        var downloadTask = new List<Task>();
        foreach (var fileSyncInfo in excepts)
        {
            await semaphore.WaitAsync();

            var t = Task.Run(async () =>
            {
                try
                {
                    await DownloadBlob(fileSyncInfo.FileName);
                }
                catch (Exception e)
                {
                    AnsiConsole.WriteException(e);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            downloadTask.Add(t);
        }

        await Task.WhenAll(downloadTask);
    }

    private static async Task DownloadBlob(string remoteFileName)
    {
        // new a BlobClient every time seems stupid...
        var client = new BlobClient(Options.ConnectionString, Options.Container, remoteFileName);
        var newFilePath = Path.Combine(Options.Path, remoteFileName);
        await client.DownloadToAsync(newFilePath);
        AnsiConsole.Write($"[{DateTime.Now}] downloaded {remoteFileName}.\n");
    }

    private static BlobContainerClient GetBlobContainer()
    {
        var container = new BlobContainerClient(Options.ConnectionString, Options.Container);
        return container;
    }
}