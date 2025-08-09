using Azure.Storage.Blobs;
using Edi.AzureBlobSync.Interfaces;

namespace Edi.AzureBlobSync.Services;

public class AzureBlobSyncService
{
    private readonly IBlobService _blobService;
    private readonly IFileService _fileService;
    private readonly IConsoleService _consoleService;
    private readonly IOptionsValidator _optionsValidator;

    private int _notDownloaded = 0;
    private int _archived = 0;

    public AzureBlobSyncService(
        IBlobService blobService,
        IFileService fileService,
        IConsoleService consoleService,
        IOptionsValidator optionsValidator)
    {
        _blobService = blobService;
        _fileService = fileService;
        _consoleService = consoleService;
        _optionsValidator = optionsValidator;
    }

    public async Task<int> RunAsync(Options options)
    {
        try
        {
            options = await _optionsValidator.ValidateAndPromptAsync(options);
            WriteParameterTable(options);

            if (!options.Silence && !_consoleService.Confirm("Good to go?"))
            {
                return 0;
            }

            var blobContainer = _blobService.CreateContainerClient(options.ConnectionString, options.Container);

            var cloudFiles = await FetchCloudFilesAsync(blobContainer, options);
            _consoleService.WriteMarkup($"[green]{cloudFiles.Count}[/] cloud file(s) found.\n");

            var localFiles = _fileService.GetLocalFiles(options.Path, options.CompareHash.GetValueOrDefault());
            _consoleService.WriteMarkup($"[green]{localFiles.Count}[/] local file(s) found.\n");

            await CompareAndSyncFilesAsync(cloudFiles, localFiles, options);

            if (!options.Silence)
            {
                _consoleService.ReadKey();
            }

            return 0;
        }
        catch (Exception ex)
        {
            _consoleService.WriteException(ex);
            if (!options.Silence)
            {
                _consoleService.ReadKey();
            }
            return 1;
        }
    }

    private void WriteParameterTable(Options options)
    {
        var parameters = new Dictionary<string, string>
        {
            { "Container Name", options.Container ?? "Not set" },
            { "Download Threads", options.Threads.ToString() },
            { "Local Path", options.Path ?? "Not set" },
            { "Keep Old", options.KeepOld.ToString() },
            { "Compare Hash", options.CompareHash.ToString() }
        };

        _consoleService.WriteTable("Edi.AzureBlobSync", parameters);
    }

    private async Task<List<FileSyncInfo>> FetchCloudFilesAsync(BlobContainerClient blobContainer, Options options)
    {
        var cloudFiles = new List<FileSyncInfo>();

        await _consoleService.StartStatusAsync("Finding files on Azure Storage...", async () =>
        {
            cloudFiles = await _blobService.GetBlobFilesAsync(blobContainer, options.CompareHash.GetValueOrDefault());
        });

        return cloudFiles;
    }

    private async Task CompareAndSyncFilesAsync(List<FileSyncInfo> cloudFiles, List<FileSyncInfo> localFiles, Options options)
    {
        var comparer = new FileSyncInfoComparer();
        var filesToDownload = cloudFiles.Except(localFiles, comparer).ToList();

        if (filesToDownload.Count != 0)
        {
            if (options.Silence || _consoleService.Confirm($"[green]{filesToDownload.Count}[/] new file(s) to download. Continue?"))
            {
                await DownloadFilesAsync(filesToDownload, options);
            }
        }
        else
        {
            _consoleService.WriteLine("No new files need to be downloaded.");
        }

        var redundantLocalFiles = localFiles.Except(cloudFiles, comparer).ToList();
        HandleRedundantLocalFiles(redundantLocalFiles, options);

        DisplaySummary(filesToDownload.Count, redundantLocalFiles.Count);
    }

    private async Task DownloadFilesAsync(List<FileSyncInfo> filesToDownload, Options options)
    {
        using var semaphore = new SemaphoreSlim(options.Threads);
        var downloadTasks = new List<Task>();

        foreach (var file in filesToDownload)
        {
            if (file.IsArchive)
            {
                Interlocked.Increment(ref _archived);
                Interlocked.Increment(ref _notDownloaded);
                _consoleService.WriteMarkup($"[yellow]Skipped archived file '{file.FileName}'.[/]\n");
                continue;
            }

            await semaphore.WaitAsync();

            downloadTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await _blobService.DownloadBlobAsync(options.ConnectionString, options.Container, file.FileName, options.Path, options.KeepOld);
                    _consoleService.WriteLine($"[{DateTime.Now:HH:mm:ss}] Downloaded {file.FileName}.");
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _notDownloaded);
                    _consoleService.WriteMarkup($"[red]Failed to download {file.FileName}: {ex.Message}[/]\n");
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(downloadTasks);
    }

    private void HandleRedundantLocalFiles(List<FileSyncInfo> redundantLocalFiles, Options options)
    {
        if (redundantLocalFiles.Count == 0)
        {
            return;
        }

        if (!options.Silence)
        {
            _consoleService.WriteMarkup($"[yellow]{redundantLocalFiles.Count}[/] redundant file(s) found. View and confirm deletion? (Press 'V' to view)[/]\n");
            var key = _consoleService.ReadKey(true);
            if (key.Key == ConsoleKey.V)
            {
                _consoleService.WriteFileTable(redundantLocalFiles);
            }
        }

        if (options.Silence || _consoleService.Confirm("[yellow]Delete these files?[/]"))
        {
            if (!options.KeepOld)
            {
                foreach (var file in redundantLocalFiles)
                {
                    var filePath = Path.Combine(options.Path, file.FileName);
                    try
                    {
                        if (_fileService.FileExists(filePath))
                        {
                            _fileService.DeleteFile(filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _consoleService.WriteMarkup($"[red]Failed to delete {file.FileName}: {ex.Message}[/]\n");
                    }
                }
            }
            else
            {
                _consoleService.WriteLine("Skipping deletion due to KeepOld option.");
            }
        }
    }

    private void DisplaySummary(int downloadedCount, int deletedCount)
    {
        _consoleService.WriteLine("----------------------------------------------------");
        _consoleService.WriteMarkup($"[green]{downloadedCount - _notDownloaded}[/] file(s) downloaded, [yellow]{deletedCount}[/] file(s) deleted, {_archived} archived file(s) skipped.\n");
    }
}