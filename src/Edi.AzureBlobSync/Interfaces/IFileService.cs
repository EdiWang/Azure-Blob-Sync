namespace Edi.AzureBlobSync.Interfaces;

public interface IFileService
{
    List<FileSyncInfo> GetLocalFiles(string path, bool compareHash);
    byte[] GetFileHash(string filePath);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    bool FileExists(string filePath);
    void DeleteFile(string filePath);
    void MoveFile(string sourcePath, string destinationPath);
    string GetDirectoryName(string path);
}