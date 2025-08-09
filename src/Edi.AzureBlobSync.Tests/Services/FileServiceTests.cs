using Edi.AzureBlobSync.Services;
using System.Security.Cryptography;
using System.Text;

namespace Edi.AzureBlobSync.Tests.Services;

[TestClass]
public class FileServiceTests
{
    private FileService _fileService;
    private string _testDirectory;
    private string _testFile1;
    private string _testFile2;

    [TestInitialize]
    public void Setup()
    {
        _fileService = new FileService();
        _testDirectory = Path.Combine(Path.GetTempPath(), "FileServiceTests", Guid.NewGuid().ToString());
        _testFile1 = Path.Combine(_testDirectory, "test1.txt");
        _testFile2 = Path.Combine(_testDirectory, "test2.txt");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [TestMethod]
    public void GetLocalFiles_NonExistentDirectory_CreatesDirectoryAndReturnsEmptyList()
    {
        // Act
        var result = _fileService.GetLocalFiles(_testDirectory, false);

        // Assert
        Assert.IsTrue(Directory.Exists(_testDirectory));
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetLocalFiles_ExistingDirectoryWithFiles_ReturnsFileSyncInfoList()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(_testFile1, "Test content 1");
        File.WriteAllText(_testFile2, "Test content 2");

        // Act
        var result = _fileService.GetLocalFiles(_testDirectory, false);

        // Assert
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Any(f => f.FileName == "test1.txt"));
        Assert.IsTrue(result.Any(f => f.FileName == "test2.txt"));
        Assert.IsTrue(result.All(f => f.Length > 0));
        Assert.IsTrue(result.All(f => string.IsNullOrEmpty(f.ContentMD5)));
    }

    [TestMethod]
    public void GetLocalFiles_WithCompareHashTrue_IncludesContentMD5()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        var testContent = "Test content for hash";
        File.WriteAllText(_testFile1, testContent);

        // Act
        var result = _fileService.GetLocalFiles(_testDirectory, true);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.IsFalse(string.IsNullOrEmpty(result[0].ContentMD5));
        
        // Verify the hash is correct
        var expectedHash = ComputeExpectedHash(testContent);
        Assert.AreEqual(expectedHash, result[0].ContentMD5);
    }

    [TestMethod]
    public void GetLocalFiles_WithCompareHashFalse_DoesNotIncludeContentMD5()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(_testFile1, "Test content");

        // Act
        var result = _fileService.GetLocalFiles(_testDirectory, false);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(string.IsNullOrEmpty(result[0].ContentMD5));
    }

    [TestMethod]
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
        CollectionAssert.AreEqual(expectedHash, result);
    }

    [TestMethod]
    [ExpectedException(typeof(FileNotFoundException))]
    public void GetFileHash_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Act
        _fileService.GetFileHash("non-existent-file.txt");
    }

    [TestMethod]
    public void DirectoryExists_ExistingDirectory_ReturnsTrue()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);

        // Act
        var result = _fileService.DirectoryExists(_testDirectory);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void DirectoryExists_NonExistentDirectory_ReturnsFalse()
    {
        // Act
        var result = _fileService.DirectoryExists(_testDirectory);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void CreateDirectory_ValidPath_CreatesDirectory()
    {
        // Act
        _fileService.CreateDirectory(_testDirectory);

        // Assert
        Assert.IsTrue(Directory.Exists(_testDirectory));
    }

    [TestMethod]
    public void FileExists_ExistingFile_ReturnsTrue()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(_testFile1, "Test content");

        // Act
        var result = _fileService.FileExists(_testFile1);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void FileExists_NonExistentFile_ReturnsFalse()
    {
        // Act
        var result = _fileService.FileExists(_testFile1);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void DeleteFile_ExistingFile_DeletesFile()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(_testFile1, "Test content");

        // Act
        _fileService.DeleteFile(_testFile1);

        // Assert
        Assert.IsFalse(File.Exists(_testFile1));
    }

    [TestMethod]
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
        Assert.IsFalse(File.Exists(_testFile1));
        Assert.IsTrue(File.Exists(destinationPath));
        Assert.AreEqual(testContent, File.ReadAllText(destinationPath));
    }

    [TestMethod]
    [ExpectedException(typeof(FileNotFoundException))]
    public void MoveFile_NonExistentSourceFile_ThrowsFileNotFoundException()
    {
        // Act
        _fileService.MoveFile("non-existent-file.txt", _testFile2);
    }

    [TestMethod]
    public void GetDirectoryName_ValidPath_ReturnsDirectoryName()
    {
        // Arrange
        var filePath = @"C:\Users\Test\Documents\file.txt";

        // Act
        var result = _fileService.GetDirectoryName(filePath);

        // Assert
        Assert.AreEqual(@"C:\Users\Test\Documents", result);
    }

    [TestMethod]
    public void GetDirectoryName_RootPath_ReturnsEmptyString()
    {
        // Arrange
        var filePath = "file.txt";

        // Act
        var result = _fileService.GetDirectoryName(filePath);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void GetDirectoryName_NullPath_ReturnsEmptyString()
    {
        // Act
        var result = _fileService.GetDirectoryName(null);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void GetLocalFiles_EmptyDirectory_ReturnsEmptyList()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);

        // Act
        var result = _fileService.GetLocalFiles(_testDirectory, false);

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
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
        Assert.AreEqual(1, result.Count);
        var fileInfo = result[0];
        Assert.AreEqual("test1.txt", fileInfo.FileName);
        Assert.AreEqual(expectedLength, fileInfo.Length);
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
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        return md5.ComputeHash(bytes);
    }
}