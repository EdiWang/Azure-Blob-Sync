using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CommandLine;
using Spectre.Console;

namespace Edi.AzureBlobSync;

class Program
{
    private static int _notDownloaded = 0;

    public static Options Options { get; set; }

    public static BlobContainerClient BlobContainer { get; set; }

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var parserResult = Parser.Default.ParseArguments<Options>(args);
        if (parserResult.Tag == ParserResultType.Parsed)
        {
            Options = ((Parsed<Options>)parserResult).Value;

            if (string.IsNullOrWhiteSpace(Options.ConnectionString))
            {
                Options.ConnectionString = AnsiConsole.Ask<string>("Enter Azure Storage Account connection string: ");
            }

            if (string.IsNullOrWhiteSpace(Options.Container))
            {
                Options.Container = AnsiConsole.Ask<string>("Enter container name: ");
            }

            if (string.IsNullOrWhiteSpace(Options.Path))
            {
                Options.Path = AnsiConsole.Ask<string>("Enter local path: ");
            }

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
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Finding files on Azure Storage...", async _ =>
                    {
                        await foreach (var blobItem in BlobContainer.GetBlobsAsync())
                        {
                            var fsi = new FileSyncInfo
                            {
                                FileName = blobItem.Name,
                                Length = blobItem.Properties.ContentLength,
                                ContentMD5 = Convert.ToBase64String(blobItem.Properties.ContentHash),
                                IsArchive = blobItem.Properties.AccessTier == AccessTier.Archive
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
                        ContentMD5 = Convert.ToBase64String(GetFileHash(fi.FullName))
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
                        await AnsiConsole.Status()
                                .Spinner(Spinner.Known.Dots)
                                .StartAsync($"Downloading files...", async _ =>
                                {
                                    await DownloadAll(excepts);
                                });
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
                            localExceptsTable.AddColumn("Content-MD5");

                            foreach (var f in localExcepts)
                            {
                                localExceptsTable.AddRow(f.FileName, f.Length.ToString(), f.ContentMD5);
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
                AnsiConsole.Write(new Markup($"Local Files Up to Date. [green]{excepts.Count - _notDownloaded}[/] new file(s) downloaded, [yellow]{deleteCount}[/] file(s) deleted."));
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
            Title = new TableTitle($"Edi.AzureBlobSync {appVersion} | .NET {Environment.Version}")
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
            if (!fileSyncInfo.IsArchive)
            {
                await semaphore.WaitAsync();

                var t = Task.Run(async () =>
                {
                    try
                    {
                        await DownloadBlob(fileSyncInfo.FileName);
                        AnsiConsole.Write($"[{DateTime.Now:HH:mm:ss}] downloaded {fileSyncInfo.FileName}, content-md5: {fileSyncInfo.ContentMD5}.\n");
                    }
                    catch (Exception e)
                    {
                        _notDownloaded++;
                        AnsiConsole.WriteException(e);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                downloadTask.Add(t);
            }
            else
            {
                _notDownloaded++;
                AnsiConsole.Write(new Markup($"[yellow]Skipped download for archived file '{fileSyncInfo.FileName}', please move it to cool or hot tier.[/]\n"));
            }
        }

        await Task.WhenAll(downloadTask);
    }

    private static async Task DownloadBlob(string remoteFileName)
    {
        var client = new BlobClient(Options.ConnectionString, Options.Container, remoteFileName);
        var newFilePath = Path.Combine(Options.Path, remoteFileName);
        await client.DownloadToAsync(newFilePath);
    }

    private static BlobContainerClient GetBlobContainer()
    {
        var container = new BlobContainerClient(Options.ConnectionString, Options.Container);
        return container;
    }

    private static byte[] GetFileHash(string path)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(path);
        return md5.ComputeHash(stream);
    }
}