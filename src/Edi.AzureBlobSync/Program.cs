using System.Reflection;
using Azure.Storage.Blobs;
using CommandLine;

namespace Edi.AzureBlobSync;

internal class Options
{
    [Option('s', Required = true, HelpText = "Storage Account Connection String")]
    public string ConnectionString { get; set; }

    [Option('c', Required = true, HelpText = "Blob Container Name")]
    public string ContainerName { get; set; }

    [Option('p', Default = "C:\\AzureBlobSyncTemp", Required = true, HelpText = "Local Folder Path")]
    public string LocalFolderPath { get; set; }

    [Option('m', Default = 10, Required = false, HelpText = "Download threads")]
    public int MaxConcurrency { get; set; }
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
            var appVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            WriteMessage("-------------------------------------------------");
            WriteMessage($" Edi.AzureBlobSync {appVersion}");
            WriteMessage($" OS Version: {Microsoft.DotNet.PlatformAbstractions.RuntimeEnvironment.OperatingSystemVersion}");
            WriteMessage("-------------------------------------------------");
            Console.WriteLine();
            WriteMessage($"Container Name: {Options.ContainerName}", ConsoleColor.DarkCyan);
            WriteMessage($"Download Threads: {Options.MaxConcurrency}", ConsoleColor.DarkCyan);
            WriteMessage($"Local Path: {Options.LocalFolderPath}", ConsoleColor.DarkCyan);
            Console.WriteLine();

            // 1. Get Azure Blob Files
            WriteMessage($"[{DateTime.Now}] Finding Files on Azure Blob Storage...");

            BlobContainer = GetBlobContainer();
            if (null == BlobContainer)
            {
                WriteMessage("ERROR: Can not get BlobContainer.", ConsoleColor.Red);
                Console.ReadKey();
                return;
            }

            try
            {
                var cloudFiles = new List<FileSyncInfo>();
                await foreach (var blobItem in BlobContainer.GetBlobsAsync())
                {
                    var fsi = new FileSyncInfo
                    {
                        FileName = blobItem.Name,
                        Length = blobItem.Properties.ContentLength
                    };
                    cloudFiles.Add(fsi);
                }

                WriteMessage($"{cloudFiles.Count} cloud file(s) found.", ConsoleColor.DarkGreen);

                // 2. Get Local Files
                if (!Directory.Exists(Options.LocalFolderPath))
                {
                    Directory.CreateDirectory(Options.LocalFolderPath);
                }

                var localFilePaths = Directory.GetFiles(Options.LocalFolderPath);
                var localFiles = localFilePaths.Select(filePath => new FileInfo(filePath))
                    .Select(fi => new FileSyncInfo
                    {
                        FileName = fi.Name,
                        Length = fi.Length,
                    })
                    .ToList();

                WriteMessage($"{localFiles.Count} local file(s) found.", ConsoleColor.DarkGreen);

                // 3. Compare Files
                WriteMessage("Comparing file meta data...");

                // Files in cloud but not in local
                var excepts = cloudFiles.Except(localFiles).ToList();
                if (excepts.Any())
                {
                    WriteMessage($"{excepts.Count} new file(s) to download. [ENTER] to continue, other key to cancel.", ConsoleColor.DarkYellow);
                    var k = Console.ReadKey();
                    Console.WriteLine();
                    if (k.Key == ConsoleKey.Enter)
                    {
                        // Download New Files
                        using var concurrencySemaphore = new SemaphoreSlim(Options.MaxConcurrency);
                        var downloadTask = new List<Task>();
                        foreach (var fileSyncInfo in excepts)
                        {
                            await concurrencySemaphore.WaitAsync();
                            //WriteMessage($"DEBUG: Concurrency Semaphore {concurrencySemaphore.CurrentCount} / {Options.MaxConcurrency}");

                            var t = Task.Run(async () =>
                            {
                                try
                                {
                                    await DownloadAsync(fileSyncInfo.FileName);
                                }
                                catch (Exception e)
                                {
                                    WriteMessage(e.Message, ConsoleColor.Red);
                                }
                                finally
                                {
                                    //WriteMessage($"DEBUG: Release concurrencySemaphore", ConsoleColor.DarkYellow);
                                    concurrencySemaphore.Release();
                                }
                            });

                            // WriteMessage($"DEBUG: Added {fileSyncInfo.FileName} ({fileSyncInfo.Length} bytes) to download tasks.", ConsoleColor.DarkYellow);
                            downloadTask.Add(t);
                        }

                        await Task.WhenAll(downloadTask);
                    }
                }
                else
                {
                    WriteMessage("No new files need to be downloaded.");
                }

                // 5. Ask Delete Old Files
                var localExcepts = localFiles.Except(cloudFiles).ToList();
                var deleteCount = 0;
                if (localExcepts.Any())
                {
                    WriteMessage($"{localExcepts.Count} redundancy file(s) exists in local but not on cloud, [V] to view file list, [ENTER] to continue.", ConsoleColor.DarkYellow);
                    var k = Console.ReadKey();
                    Console.WriteLine();
                    if (k.Key == ConsoleKey.V)
                    {
                        foreach (var f in localExcepts)
                        {
                            Console.WriteLine($"{f.FileName}\t{f.Length} bytes");
                        }
                    }

                    var k1 = Console.ReadKey();
                    Console.WriteLine();
                    if (k1.Key == ConsoleKey.Enter)
                    {
                        WriteMessage($"Do you want to delete these files? [Y/N]", ConsoleColor.DarkYellow);
                        var k2 = Console.ReadKey();
                        Console.WriteLine();
                        if (k2.Key == ConsoleKey.Y)
                        {
                            WriteMessage("Deleting local redundancy files...");
                            foreach (var fi in localExcepts)
                            {
                                File.Delete(Path.Combine(Options.LocalFolderPath, fi.FileName));
                                deleteCount++;
                            }
                        }
                    }
                }

                WriteMessage("----------------------------------------------------");
                WriteMessage($"Local Files Up to Date. {excepts.Count} new file(s) downloaded, {deleteCount} file(s) deleted.", ConsoleColor.Green);
            }
            catch (Exception e)
            {
                WriteMessage(e.Message, ConsoleColor.Red);
            }
        }
        else
        {
            WriteMessage("ERROR: Failed to parse console parameters.", ConsoleColor.Red);
        }

        Console.ReadKey();
    }

    private static void WriteMessage(string message, ConsoleColor color = ConsoleColor.White, bool resetColor = true)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        if (resetColor)
        {
            // Why some items showing default console color while most items works fine (white) in DownloadAsync()...
            // Isn't this thread safe?
            // Who cares anyway...
            Console.ResetColor();
        }
    }

    private static async Task DownloadAsync(string remoteFileName)
    {
        // new a BlobClient every time seems stupid...
        var client = new BlobClient(Options.ConnectionString, Options.ContainerName, remoteFileName);
        var newFilePath = Path.Combine(Options.LocalFolderPath, remoteFileName);
        await client.DownloadToAsync(newFilePath);
        WriteMessage($"[{DateTime.Now}] downloaded {remoteFileName}.");
    }

    private static BlobContainerClient GetBlobContainer()
    {
        var container = new BlobContainerClient(Options.ConnectionString, Options.ContainerName);
        return container;
    }
}