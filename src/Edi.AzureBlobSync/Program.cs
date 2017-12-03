using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                var localFilePaths = Directory.GetFiles(Options.LocalFolderPath);
                var localFiles = localFilePaths.Select(filePath => new FileInfo(filePath))
                                               .Select(fi => new FileSyncInfo
                                               {
                                                   FileName = fi.Name,
                                                   Length = fi.Length,
                                               })
                                               .ToList();

                Console.WriteLine($"{localFiles.Count} local file(s) found.");

                // 3. Compare Files
                Console.WriteLine($"Comparing file meta data...");
                Console.WriteLine("----------------------------------------------------");

                // Files in cloud but not in local
                var excepts = cloudFiles.Except(localFiles).ToList();
                if (excepts.Any())
                {
                    Console.WriteLine($"{excepts.Count} new file(s) to download. [ENTER] to continue, other key to cancel.");
                    var k = Console.ReadKey();
                    if (k.Key == ConsoleKey.Enter)
                    {
                        // Download New Files
                        var downloadTask = new List<Task>();
                        foreach (var fileSyncInfo in excepts)
                        {
                            Console.WriteLine($"Added {fileSyncInfo.FileName} ({fileSyncInfo.Length} bytes) to download.");
                            downloadTask.Add(DownloadAsync(fileSyncInfo.FileName));
                        }
                        await Task.WhenAll(downloadTask);
                    }
                }
                else
                {
                    Console.WriteLine($"No new files need to be downloaded.");
                }

                // 5. Ask Delete Old Files
                var localExcepts = localFiles.Except(cloudFiles).ToList();
                var deleteCount = 0;
                if (localExcepts.Any())
                {
                    Console.WriteLine($"{localExcepts.Count} redundancy file(s) exists in local but not on cloud, [V] to view file list, [ENTER] to continue.");
                    var k = Console.ReadKey();
                    if (k.Key == ConsoleKey.V)
                    {
                        foreach (var f in localExcepts)
                        {
                            Console.WriteLine($"{f.FileName}\t{f.Length} bytes");
                        }
                    }
                    if (k.Key == ConsoleKey.Enter)
                    {
                        Console.WriteLine($"Do you want to delete these files? [Y/N]");
                        var k1 = Console.ReadKey();
                        if (k1.Key == ConsoleKey.Y)
                        {
                            Console.WriteLine("Deleting local redundancy files...");
                            foreach (var fi in localExcepts)
                            {
                                File.Delete(Path.Combine(Options.LocalFolderPath, fi.FileName));
                                deleteCount++;
                            }
                        }
                    }
                }

                Console.WriteLine("----------------------------------------------------");
                Console.WriteLine($"Local Files Up to Date. {excepts.Count} new file(s) downloaded, {deleteCount} file(s) deleted.");
                Console.ReadLine();
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
