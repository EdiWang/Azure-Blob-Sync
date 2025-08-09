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
    private static int _archived = 0;

    public static Options Options { get; private set; } = null!;
    public static BlobContainerClient BlobContainer { get; private set; } = null!;

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var parserResult = Parser.Default.ParseArguments<Options>(args);

        if (parserResult is Parsed<Options> parsedResult)
        {
            Options = parsedResult.Value;
            return await RunAsync();
        }
        else
        {
            AnsiConsole.Write(new Markup("[red]ERROR: Failed to parse console parameters.[/]"));
            if (!Options?.Silence ?? true)
            {
                Console.ReadKey();
            }
            return 1;
        }
    }

    private static async Task<int> RunAsync()
    {
        try
        {
            ValidateAndPromptOptions();
            WriteParameterTable();

            if (!Options.Silence && !AnsiConsole.Confirm("Good to go?"))
            {
                return 0;
            }

            BlobContainer = InitializeBlobContainer();

            var cloudFiles = await FetchCloudFilesAsync();
            AnsiConsole.Write(new Markup($"[green]{cloudFiles.Count}[/] cloud file(s) found.\n"));

            var localFiles = FetchLocalFiles();
            AnsiConsole.Write(new Markup($"[green]{localFiles.Count}[/] local file(s) found.\n"));

            await CompareAndSyncFilesAsync(cloudFiles, localFiles);

            if (!Options.Silence)
            {
                Console.ReadKey();
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            if (!Options.Silence)
            {
                Console.ReadKey();
            }
            return 1;
        }
    }

    #region Validation and Setup

    private static void ValidateAndPromptOptions()
    {
        Options.ConnectionString ??= AnsiConsole.Ask<string>("Enter Azure Storage Account connection string: ");
        Options.Container ??= AnsiConsole.Ask<string>("Enter container name: ");
        Options.Path ??= AnsiConsole.Ask<string>("Enter local path: ");

        // Validate connection string format
        if (!Options.ConnectionString.Contains("AccountName=") || !Options.ConnectionString.Contains("AccountKey="))
        {
            throw new ArgumentException("Invalid connection string format.");
        }

        // Ensure path is absolute
        if (!Path.IsPathRooted(Options.Path))
        {
            Options.Path = Path.GetFullPath(Options.Path);
        }
    }

    private static BlobContainerClient InitializeBlobContainer()
    {
        var client = new BlobContainerClient(Options.ConnectionString, Options.Container);

        // Verify container exists
        if (!client.Exists())
        {
            throw new InvalidOperationException($"Container '{Options.Container}' does not exist or is not accessible.");
        }

        return client;
    }

    private static void WriteParameterTable()
    {
        var appVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";

        var table = new Table
        {
            Title = new($"Edi.AzureBlobSync {appVersion} | .NET {Environment.Version}")
        };

        table.AddColumn("Parameter");
        table.AddColumn("Value");
        table.AddRow("[blue]Container Name[/]", Options.Container ?? "Not set");
        table.AddRow("[blue]Download Threads[/]", Options.Threads.ToString());
        table.AddRow("[blue]Local Path[/]", Options.Path ?? "Not set");
        table.AddRow("[blue]Keep Old[/]", Options.KeepOld.ToString());
        table.AddRow("[blue]Compare Hash[/]", Options.CompareHash.ToString());

        AnsiConsole.Write(table);
    }

    #endregion

    #region File Retrieval

    private static async Task<List<FileSyncInfo>> FetchCloudFilesAsync()
    {
        var cloudFiles = new List<FileSyncInfo>();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Finding files on Azure Storage...", async _ =>
            {
                var asyncEnumerable = BlobContainer.GetBlobsAsync(cancellationToken: CancellationToken.None);
                await foreach (var blobItem in asyncEnumerable.ConfigureAwait(false))
                {
                    cloudFiles.Add(new FileSyncInfo
                    {
                        FileName = blobItem.Name,
                        Length = blobItem.Properties.ContentLength,
                        ContentMD5 = Options.CompareHash.GetValueOrDefault() && blobItem.Properties.ContentHash != null
                            ? Convert.ToBase64String(blobItem.Properties.ContentHash)
                            : string.Empty,
                        IsArchive = blobItem.Properties.AccessTier == AccessTier.Archive
                    });
                }
            });

        return cloudFiles;
    }

    private static List<FileSyncInfo> FetchLocalFiles()
    {
        if (!Directory.Exists(Options.Path))
        {
            Directory.CreateDirectory(Options.Path);
            return new List<FileSyncInfo>();
        }

        return Directory.GetFiles(Options.Path, "*", SearchOption.TopDirectoryOnly)
            .AsParallel()
            .Select(filePath => new FileInfo(filePath))
            .Where(fileInfo => fileInfo.Exists) // Additional safety check
            .Select(fileInfo => new FileSyncInfo
            {
                FileName = fileInfo.Name,
                Length = fileInfo.Length,
                ContentMD5 = Options.CompareHash.GetValueOrDefault()
                    ? Convert.ToBase64String(GetFileHash(fileInfo.FullName))
                    : string.Empty
            })
            .ToList();
    }

    #endregion

    #region File Comparison and Synchronization

    private static async Task CompareAndSyncFilesAsync(List<FileSyncInfo> cloudFiles, List<FileSyncInfo> localFiles)
    {
        // Use a comparer that handles case-insensitive file name comparison
        var comparer = new FileSyncInfoComparer();
        var filesToDownload = cloudFiles.Except(localFiles, comparer).ToList();

        if (filesToDownload.Count != 0)
        {
            if (Options.Silence || AnsiConsole.Confirm($"[green]{filesToDownload.Count}[/] new file(s) to download. Continue?"))
            {
                await DownloadFilesAsync(filesToDownload);
            }
        }
        else
        {
            AnsiConsole.WriteLine("No new files need to be downloaded.");
        }

        var redundantLocalFiles = localFiles.Except(cloudFiles, comparer).ToList();
        HandleRedundantLocalFiles(redundantLocalFiles);

        DisplaySummary(filesToDownload.Count, redundantLocalFiles.Count);
    }

    private static async Task DownloadFilesAsync(List<FileSyncInfo> filesToDownload)
    {
        using var semaphore = new SemaphoreSlim(Options.Threads);
        var downloadTasks = new List<Task>();

        foreach (var file in filesToDownload)
        {
            if (file.IsArchive)
            {
                Interlocked.Increment(ref _notDownloaded);
                Interlocked.Increment(ref _archived);
                AnsiConsole.Write(new Markup($"[yellow]Skipped archived file '{file.FileName}'.[/]\n"));
                continue;
            }

            await semaphore.WaitAsync();

            downloadTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await DownloadBlobAsync(file.FileName);
                    AnsiConsole.Write($"[{DateTime.Now:HH:mm:ss}] Downloaded {file.FileName}.\n");
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _notDownloaded);
                    AnsiConsole.Write(new Markup($"[red]Failed to download {file.FileName}: {ex.Message}[/]\n"));
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(downloadTasks);
    }

    private static void HandleRedundantLocalFiles(List<FileSyncInfo> redundantLocalFiles)
    {
        if (redundantLocalFiles.Count == 0)
        {
            return;
        }

        if (!Options.Silence)
        {
            AnsiConsole.Write(new Markup($"[yellow]{redundantLocalFiles.Count}[/] redundant file(s) found. View and confirm deletion? (Press 'V' to view)[/]\n"));
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.V)
            {
                DisplayFileList(redundantLocalFiles);
            }
        }

        if (Options.Silence || AnsiConsole.Confirm("[yellow]Delete these files?[/]"))
        {
            if (!Options.KeepOld)
            {
                foreach (var file in redundantLocalFiles)
                {
                    var filePath = Path.Combine(Options.Path, file.FileName);
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.Write(new Markup($"[red]Failed to delete {file.FileName}: {ex.Message}[/]\n"));
                    }
                }
            }
            else
            {
                AnsiConsole.WriteLine("Skipping deletion due to KeepOld option.");
            }
        }
    }

    private static void DisplayFileList(IEnumerable<FileSyncInfo> files)
    {
        var table = new Table();
        table.AddColumn("File Name");
        table.AddColumn("Length (bytes)");
        table.AddColumn("Content-MD5");

        foreach (var file in files)
        {
            table.AddRow(
                file.FileName ?? "Unknown",
                file.Length?.ToString() ?? "Unknown",
                string.IsNullOrEmpty(file.ContentMD5) ? "Not calculated" : file.ContentMD5);
        }

        AnsiConsole.Write(table);
    }

    private static void DisplaySummary(int downloadedCount, int deletedCount)
    {
        AnsiConsole.WriteLine("----------------------------------------------------");
        AnsiConsole.Write(new Markup($"[green]{downloadedCount - _notDownloaded}[/] file(s) downloaded, [yellow]{deletedCount}[/] file(s) deleted, {_archived} archived file(s) skipped.\n"));
    }

    #endregion

    #region Blob Operations

    private static async Task DownloadBlobAsync(string remoteFileName)
    {
        var client = new BlobClient(Options.ConnectionString, Options.Container, remoteFileName);
        var localFilePath = Path.Combine(Options.Path, remoteFileName);

        // Ensure directory exists for nested file paths
        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(localFilePath) && Options.KeepOld)
        {
            var timestampedFileName = $"{Path.GetFileNameWithoutExtension(localFilePath)}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(localFilePath)}";
            var timestampedFilePath = Path.Combine(Options.Path, timestampedFileName);
            File.Move(localFilePath, timestampedFilePath);

            AnsiConsole.Write(new Markup($"[yellow]Renamed existing file to '{timestampedFileName}'.[/]\n"));
        }

        await client.DownloadToAsync(localFilePath);
    }

    private static byte[] GetFileHash(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        return md5.ComputeHash(stream);
    }

    #endregion
}
