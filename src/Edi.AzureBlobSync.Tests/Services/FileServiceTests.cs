using Edi.AzureBlobSync.Services;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Edi.AzureBlobSync.Tests.Services;

public class FileServiceTests : IDisposable
{
    private readonly FileService _fileService;
    private readonly string _testDirectory;
    private readonly string _testFile1;
    private readonly string _testFile2;

    public FileServiceTests()
    {
        _fileService = new FileService();
        _testDirectory = Path.Combine(Path.GetTempPath(), "FileServiceTests", Guid.NewGuid().ToString());
        _testFile1 = Path.Combine(_testDirectory, "test1.txt");
        _testFile2 = Path.Combine(_testDirectory, "test2.txt");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void GetLocalFiles_NonExistentDirectory_CreatesDirectoryAndReturnsEmptyList()
    {
        // Act
        var result = _fileService.GetLocalFiles(_testDirectory, false);

        // Assert
        Assert.True(Directory.Exists(_testDirectory));
        Assert.Empty(result);
    }

    [Fact]
    public void GetLocalFiles_ExistingDirectoryWithFiles_ReturnsFileSyncInfoList()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(_testFile1, "Test content 1");
        File.WriteAllText(_testFile2, "Test content 2");

        // Act
        var result = _fileService.GetLocalFiles(_testDirectory, false);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, f => f.FileName == "test1.txt");
        Assert.Contains(result, f => f.FileName == "test2.txt");
        Assert.True(result.All(f => f.Length > 0));
        Assert.True(result.All(f => string.IsNullOrEmpty(f.ContentMD5)));
    }

    [Fact]
    public void GetLocalFiles_WithCompareHashTrue_IncludesContentMD5()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        var testContent = "Test content for hash";
        File.WriteAllText(_testFile1, testContent);

        // Act
        var result = _fileService.GetLocalFiles(_testDirectory, true);

        // Assert
        Assert.Single(result);
        Assert.False(string.IsNullOrEmpty(result[0].ContentMD5));
        
        // Verify the hash is correct
        var expectedHash = ComputeExpectedHash(testContent);
        Assert.Equal(expectedHash, result[0].ContentMD5);
    }

    [Fact]
    public void GetLocalFiles_WithCompareHashFalse_DoesNotIncludeContentMD5()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(_testFile1, "Test content");

        // Act
        var result = _fileService.GetLocalFiles(_testDirectory, false);

        // Assert
        Assert.Single(result);
        Assert.True(string.IsNullOrEmpty(result[0].ContentMD5));
    }

    [Fact]
    public void GetFileHash_ValidFile_ReturnsCorrectHash()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        var testContent = "Test content for hash calculation";
        File.WriteAllText(_testFile1, testContent);

        // Act
        var result = _fileService.GetFileHash(_testFile1);

        // Assert
        var expectedHash = ComputeExpectedHashBytes(testContent);
        Assert.Equal(expectedHash, result);
    }

    [Fact]
    public void GetFileHash_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _fileService.GetFileHash("non-existent-file.txt"));
    }

    [Fact]
    public void DirectoryExists_ExistingDirectory_ReturnsTrue()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);

        // Act
        var result = _fileService.DirectoryExists(_testDirectory);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DirectoryExists_NonExistentDirectory_ReturnsFalse()
    {
        // Act
        var result = _fileService.DirectoryExists(_testDirectory);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CreateDirectory_ValidPath_CreatesDirectory()
    {
        // Act
        _fileService.CreateDirectory(_testDirectory);

        // Assert
        Assert.True(Directory.Exists(_testDirectory));
    }

    [Fact]
    public void FileExists_ExistingFile_ReturnsTrue()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(_testFile1, "Test content");

        // Act
        var result = _fileService.FileExists(_testFile1);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void FileExists_NonExistentFile_ReturnsFalse()
    {
        // Act
        var result = _fileService.FileExists(_testFile1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DeleteFile_ExistingFile_DeletesFile()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(_testFile1, "Test content");

        // Act
        _fileService.DeleteFile(_testFile1);

        // Assert
        Assert.False(File.Exists(_testFile1));
    }

    [Fact]
    public void MoveFile_ValidPaths_MovesFile()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        var testContent = "Test content";
        File.WriteAllText(_testFile1, testContent);
        var destinationPath = Path.Combine(_testDirectory, "moved-file.txt");

        // Act
        _fileService.MoveFile(_testFile1, destinationPath);

        // Assert
        Assert.False(File.Exists(_testFile1));
        Assert.True(File.Exists(destinationPath));
        Assert.Equal(testContent, File.ReadAllText(destinationPath));
    }

    [Fact]
    public void MoveFile_NonExistentSourceFile_ThrowsFileNotFoundException()
    {
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _fileService.MoveFile("non-existent-file.txt", _testFile2));
    }

    [Fact]
    public void GetDirectoryName_ValidPath_ReturnsDirectoryName()
    {
        // Arrange
        var filePath = @"C:\Users\Test\Documents\file.txt";

        // Act
        var result = _fileService.GetDirectoryName(filePath);

        // Assert
        Assert.Equal(@"C:\Users\Test\Documents", result);
    }

    [Fact]
    public void GetDirectoryName_RootPath_ReturnsEmptyString()
    {
        // Arrange
        var filePath = "file.txt";

        // Act
        var result = _fileService.GetDirectoryName(filePath);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetDirectoryName_NullPath_ReturnsEmptyString()
    {
        // Act
        var result = _fileService.GetDirectoryName(null);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetLocalFiles_EmptyDirectory_ReturnsEmptyList()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);

        // Act
        var result = _fileService.GetLocalFiles(_testDirectory, false);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetLocalFiles_VerifyFileProperties_ReturnsCorrectFileInfo()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        var testContent = "This is test content with specific length";
        File.WriteAllText(_testFile1, testContent);
        var expectedLength = Encoding.UTF8.GetBytes(testContent).Length;

        // Act
        var result = _fileService.GetLocalFiles(_testDirectory, false);

        // Assert
        Assert.Single(result);
        var fileInfo = result[0];
        Assert.Equal("test1.txt", fileInfo.FileName);
        Assert.Equal(expectedLength, fileInfo.Length);
    }

    private static string ComputeExpectedHash(string content)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = md5.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static byte[] ComputeExpectedHashBytes(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return MD5.HashData(bytes);
    }
}