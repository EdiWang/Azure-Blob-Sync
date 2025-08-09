using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Edi.AzureBlobSync.Interfaces;

namespace Edi.AzureBlobSync.Services;

public class BlobService : IBlobService
{
    public BlobContainerClient CreateContainerClient(string connectionString, string containerName)
    {
        var client = new BlobContainerClient(connectionString, containerName);

        if (!client.Exists())
        {
            throw new InvalidOperationException($"Container '{containerName}' does not exist or is not accessible.");
        }

        return client;
    }

    public async Task<List<FileSyncInfo>> GetBlobFilesAsync(BlobContainerClient containerClient, bool compareHash, CancellationToken cancellationToken = default)
    {
        var cloudFiles = new List<FileSyncInfo>();
        var asyncEnumerable = containerClient.GetBlobsAsync(cancellationToken: cancellationToken);

        await foreach (var blobItem in asyncEnumerable.ConfigureAwait(false))
        {
            cloudFiles.Add(new FileSyncInfo
            {
                FileName = blobItem.Name,
                Length = blobItem.Properties.ContentLength,
                ContentMD5 = compareHash && blobItem.Properties.ContentHash != null
                    ? Convert.ToBase64String(blobItem.Properties.ContentHash)
                    : string.Empty,
                IsArchive = blobItem.Properties.AccessTier == AccessTier.Archive
            });
        }

        return cloudFiles;
    }

    public async Task DownloadBlobAsync(string connectionString, string containerName, string fileName, string localPath, bool keepOld)
    {
        var client = new BlobClient(connectionString, containerName, fileName);
        var localFilePath = Path.Combine(localPath, fileName);

        // Ensure directory exists for nested file paths
        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(localFilePath) && keepOld)
        {
            var timestampedFileName = $"{Path.GetFileNameWithoutExtension(localFilePath)}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(localFilePath)}";
            var timestampedFilePath = Path.Combine(localPath, timestampedFileName);
            File.Move(localFilePath, timestampedFilePath);
        }

        await client.DownloadToAsync(localFilePath);
    }
}