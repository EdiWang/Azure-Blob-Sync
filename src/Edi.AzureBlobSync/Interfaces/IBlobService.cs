using Azure.Storage.Blobs;

namespace Edi.AzureBlobSync.Interfaces;

public interface IBlobService
{
    BlobContainerClient CreateContainerClient(string connectionString, string containerName);
    Task<List<FileSyncInfo>> GetBlobFilesAsync(BlobContainerClient containerClient, bool compareHash, CancellationToken cancellationToken = default);
    Task DownloadBlobAsync(string connectionString, string containerName, string fileName, string localPath, bool keepOld);
}