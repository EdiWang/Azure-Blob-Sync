using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Edi.AzureBlobSync
{
    class Options
    {
        [Option('n', Required = true, HelpText = "Storage Account Name")]
        public string AccountName { get; set; }

        [Option('k', Required = true, HelpText = "Storage Account Key")]
        public string AccountKey { get; set; }

        [Option('c', Required = true, HelpText = "Blob Container Name")]
        public string ContainerName { get; set; }

        [Option('p', Default = "C:\\AzureBlobSyncTemp", Required = true, HelpText = "Local Folder Path")]
        public string LocalFolderPath { get; set; }

        [Option('m', Default = 10, Required = false, HelpText = "Download threads")]
        public int MaxConcurrency { get; set; }
    }

    class FileSyncInfo
    {
        public string FileName { get; set; }

        public long Length { get; set; }

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
            return $"{FileName}{Length}".Length;
        }
    }

    class Program
    {
        public static Options Options { get; set; }

        public static CloudBlobContainer BlobContainer { get; set; }

        public static async Task Main(string[] args)
        {
            var parserResult = Parser.Default.ParseArguments<Options>(args);
            if (parserResult.Tag == ParserResultType.Parsed)
            {
                Options = ((Parsed<Options>)parserResult).Value;

                // 1. Get Azure Blob Files
                Console.WriteLine("Finding Files on Azure Blob Storage...");

                BlobContainer = GetBlobContainer();
                if (null == BlobContainer)
                {
                    WriteMessage("ERROR: Can not get BlobContainer.", ConsoleColor.Red);
                    Console.ReadKey();
                    return;
                }

                try
                {
                    var blobs = await BlobContainer.ListBlobsSegmentedAsync(null);
                    var cloudFiles = (from item in blobs.Results
                                      where item.GetType() == typeof(CloudBlockBlob)
                                      select (CloudBlockBlob)item
                        into blob
                                      select new FileSyncInfo()
                                      {
                                          FileName = blob.Name,
                                          Length = blob.Properties.Length
                                      }).ToList();

                    Console.WriteLine($"{cloudFiles.Count} cloud file(s) found.");

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

                    WriteMessage($"{localFiles.Count} local file(s) found.");

                    // 3. Compare Files
                    WriteMessage("Comparing file meta data...");
                    WriteMessage("----------------------------------------------------");

                    // Files in cloud but not in local
                    var excepts = cloudFiles.Except(localFiles).ToList();
                    if (excepts.Any())
                    {
                        WriteMessage($"{excepts.Count} new file(s) to download. [ENTER] to continue, other key to cancel.", ConsoleColor.Yellow);
                        var k = Console.ReadKey();
                        Console.WriteLine();
                        if (k.Key == ConsoleKey.Enter)
                        {
                            // Download New Files
                            using (var concurrencySemaphore = new SemaphoreSlim(Options.MaxConcurrency))
                            {
                                var downloadTask = new List<Task>();
                                foreach (var fileSyncInfo in excepts)
                                {
                                    concurrencySemaphore.Wait();
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

                                    WriteMessage($"Added {fileSyncInfo.FileName} ({fileSyncInfo.Length} bytes) to download tasks.");
                                    downloadTask.Add(t);
                                }

                                await Task.WhenAll(downloadTask);
                            }
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
                        WriteMessage($"{localExcepts.Count} redundancy file(s) exists in local but not on cloud, [V] to view file list, [ENTER] to continue.", ConsoleColor.Yellow);
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
                            WriteMessage($"Do you want to delete these files? [Y/N]", ConsoleColor.Yellow);
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
                    Console.ReadLine();
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
                Console.ResetColor();
            }
        }

        private static async Task DownloadAsync(string remoteFileName)
        {
            CloudBlockBlob blockBlob = BlobContainer.GetBlockBlobReference(remoteFileName);
            var newFilePath = Path.Combine(Options.LocalFolderPath, remoteFileName);
            await blockBlob.DownloadToFileAsync(newFilePath, FileMode.Create);
            Console.WriteLine($"[{DateTime.Now}] {remoteFileName} downloaded.");
        }

        private static CloudBlobContainer GetBlobContainer()
        {
            CloudStorageAccount storageAccount = new CloudStorageAccount(new StorageCredentials(Options.AccountName, Options.AccountKey), true);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(Options.ContainerName);
            return container;
        }
    }
}
