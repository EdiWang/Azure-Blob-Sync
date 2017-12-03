using System;
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

    class BlobFile
    {
        public string FileName { get; set; }

        public DateTimeOffset? LastModified { get; set; }
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

                Console.WriteLine("Finding Files on Azure Blob Storage...");
                BlobContainer = GetBlobContainer();
                var blobs = await BlobContainer.ListBlobsSegmentedAsync(null);
                var cloudFiles = (from item in blobs.Results
                                  where item.GetType() == typeof(CloudBlockBlob)
                                  select (CloudBlockBlob)item
                                  into blob
                                  select new BlobFile()
                                  {
                                      LastModified = blob.Properties.LastModified,
                                      FileName = blob.Name
                                  }).ToList();

                Console.WriteLine($"{cloudFiles.Count} file(s) found.");

                Console.ReadLine();
            }
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
