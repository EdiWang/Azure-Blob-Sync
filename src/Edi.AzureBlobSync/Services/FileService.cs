using System.Security.Cryptography;
using Edi.AzureBlobSync.Interfaces;

namespace Edi.AzureBlobSync.Services;

public class FileService : IFileService
{
    public List<FileSyncInfo> GetLocalFiles(string path, bool compareHash)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            return new List<FileSyncInfo>();
        }

        return Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly)
            .AsParallel()
            .Select(filePath => new FileInfo(filePath))
            .Where(fileInfo => fileInfo.Exists)
            .Select(fileInfo => new FileSyncInfo
            {
                FileName = fileInfo.Name,
                Length = fileInfo.Length,
                ContentMD5 = compareHash ? Convert.ToBase64String(GetFileHash(fileInfo.FullName)) : string.Empty
            })
            .ToList();
    }

    public byte[] GetFileHash(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        return md5.ComputeHash(stream);
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public bool FileExists(string filePath) => File.Exists(filePath);

    public void DeleteFile(string filePath) => File.Delete(filePath);

    public void MoveFile(string sourcePath, string destinationPath) => File.Move(sourcePath, destinationPath);

    public string GetDirectoryName(string path) => Path.GetDirectoryName(path) ?? string.Empty;
}