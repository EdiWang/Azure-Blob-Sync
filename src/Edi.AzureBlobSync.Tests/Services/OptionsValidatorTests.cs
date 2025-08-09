using Edi.AzureBlobSync.Interfaces;
using Edi.AzureBlobSync.Services;
using Moq;

namespace Edi.AzureBlobSync.Tests.Services;

[TestClass]
public class OptionsValidatorTests
{
    private Mock<IConsoleService> _mockConsoleService;
    private OptionsValidator _optionsValidator;

    [TestInitialize]
    public void Setup()
    {
        _mockConsoleService = new Mock<IConsoleService>();
        _optionsValidator = new OptionsValidator(_mockConsoleService.Object);
    }

    [TestMethod]
    public void ValidateAndPrompt_WithAllPropertiesSet_ReturnsOptionsUnchanged()
    {
        // Arrange
        var options = new Options
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            Container = "testcontainer",
            Path = @"C:\TestPath"
        };

        // Act
        var result = _optionsValidator.ValidateAndPrompt(options);

        // Assert
        Assert.AreEqual(options.ConnectionString, result.ConnectionString);
        Assert.AreEqual(options.Container, result.Container);
        Assert.AreEqual(options.Path, result.Path);
        _mockConsoleService.Verify(x => x.Ask(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void ValidateAndPrompt_WithNullConnectionString_PromptsForConnectionString()
    {
        // Arrange
        var options = new Options
        {
            ConnectionString = null,
            Container = "testcontainer",
            Path = @"C:\TestPath"
        };

        var expectedConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net";
        _mockConsoleService
            .Setup(x => x.Ask("Enter Azure Storage Account connection string: "))
            .Returns(expectedConnectionString);

        // Act
        var result = _optionsValidator.ValidateAndPrompt(options);

        // Assert
        Assert.AreEqual(expectedConnectionString, result.ConnectionString);
        _mockConsoleService.Verify(x => x.Ask("Enter Azure Storage Account connection string: "), Times.Once);
    }

    [TestMethod]
    public void ValidateAndPrompt_WithNullContainer_PromptsForContainer()
    {
        // Arrange
        var options = new Options
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            Container = null,
            Path = @"C:\TestPath"
        };

        var expectedContainer = "testcontainer";
        _mockConsoleService
            .Setup(x => x.Ask("Enter container name: "))
            .Returns(expectedContainer);

        // Act
        var result = _optionsValidator.ValidateAndPrompt(options);

        // Assert
        Assert.AreEqual(expectedContainer, result.Container);
        _mockConsoleService.Verify(x => x.Ask("Enter container name: "), Times.Once);
    }

    [TestMethod]
    public void ValidateAndPrompt_WithNullPath_PromptsForPath()
    {
        // Arrange
        var options = new Options
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            Container = "testcontainer",
            Path = null
        };

        var expectedPath = @"C:\TestPath";
        _mockConsoleService
            .Setup(x => x.Ask("Enter local path: "))
            .Returns(expectedPath);

        // Act
        var result = _optionsValidator.ValidateAndPrompt(options);

        // Assert
        Assert.AreEqual(expectedPath, result.Path);
        _mockConsoleService.Verify(x => x.Ask("Enter local path: "), Times.Once);
    }

    [TestMethod]
    public void ValidateAndPrompt_WithAllNullProperties_PromptsForAll()
    {
        // Arrange
        var options = new Options
        {
            ConnectionString = null,
            Container = null,
            Path = null
        };

        var expectedConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net";
        var expectedContainer = "testcontainer";
        var expectedPath = @"C:\TestPath";

        _mockConsoleService
            .Setup(x => x.Ask("Enter Azure Storage Account connection string: "))
            .Returns(expectedConnectionString);
        _mockConsoleService
            .Setup(x => x.Ask("Enter container name: "))
            .Returns(expectedContainer);
        _mockConsoleService
            .Setup(x => x.Ask("Enter local path: "))
            .Returns(expectedPath);

        // Act
        var result = _optionsValidator.ValidateAndPrompt(options);

        // Assert
        Assert.AreEqual(expectedConnectionString, result.ConnectionString);
        Assert.AreEqual(expectedContainer, result.Container);
        Assert.AreEqual(expectedPath, result.Path);
        _mockConsoleService.Verify(x => x.Ask("Enter Azure Storage Account connection string: "), Times.Once);
        _mockConsoleService.Verify(x => x.Ask("Enter container name: "), Times.Once);
        _mockConsoleService.Verify(x => x.Ask("Enter local path: "), Times.Once);
    }

    [TestMethod]
    public void ValidateAndPrompt_WithInvalidConnectionStringMissingAccountName_ThrowsArgumentException()
    {
        // Arrange
        var options = new Options
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            Container = "testcontainer",
            Path = @"C:\TestPath"
        };

        // Act & Assert
        var exception = Assert.ThrowsException<ArgumentException>(() => _optionsValidator.ValidateAndPrompt(options));
        Assert.AreEqual("Invalid connection string format.", exception.Message);
    }

    [TestMethod]
    public void ValidateAndPrompt_WithInvalidConnectionStringMissingAccountKey_ThrowsArgumentException()
    {
        // Arrange
        var options = new Options
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;EndpointSuffix=core.windows.net",
            Container = "testcontainer",
            Path = @"C:\TestPath"
        };

        // Act & Assert
        var exception = Assert.ThrowsException<ArgumentException>(() => _optionsValidator.ValidateAndPrompt(options));
        Assert.AreEqual("Invalid connection string format.", exception.Message);
    }

    [TestMethod]
    public void ValidateAndPrompt_WithInvalidConnectionStringMissingBoth_ThrowsArgumentException()
    {
        // Arrange
        var options = new Options
        {
            ConnectionString = "DefaultEndpointsProtocol=https;EndpointSuffix=core.windows.net",
            Container = "testcontainer",
            Path = @"C:\TestPath"
        };

        // Act & Assert
        var exception = Assert.ThrowsException<ArgumentException>(() => _optionsValidator.ValidateAndPrompt(options));
        Assert.AreEqual("Invalid connection string format.", exception.Message);
    }

    [TestMethod]
    public void ValidateAndPrompt_WithRelativePath_ConvertsToAbsolutePath()
    {
        // Arrange
        var relativePath = "TestFolder";
        var options = new Options
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            Container = "testcontainer",
            Path = relativePath
        };

        // Act
        var result = _optionsValidator.ValidateAndPrompt(options);

        // Assert
        Assert.IsTrue(Path.IsPathRooted(result.Path));
        Assert.AreEqual(Path.GetFullPath(relativePath), result.Path);
    }

    [TestMethod]
    public void ValidateAndPrompt_WithAbsolutePath_KeepsPathUnchanged()
    {
        // Arrange
        var absolutePath = @"C:\TestPath";
        var options = new Options
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            Container = "testcontainer",
            Path = absolutePath
        };

        // Act
        var result = _optionsValidator.ValidateAndPrompt(options);

        // Assert
        Assert.AreEqual(absolutePath, result.Path);
    }

    [TestMethod]
    public void ValidateAndPrompt_WithPromptedInvalidConnectionString_ThrowsArgumentException()
    {
        // Arrange
        var options = new Options
        {
            ConnectionString = null,
            Container = "testcontainer",
            Path = @"C:\TestPath"
        };

        var invalidConnectionString = "InvalidConnectionString";
        _mockConsoleService
            .Setup(x => x.Ask("Enter Azure Storage Account connection string: "))
            .Returns(invalidConnectionString);

        // Act & Assert
        var exception = Assert.ThrowsException<ArgumentException>(() => _optionsValidator.ValidateAndPrompt(options));
        Assert.AreEqual("Invalid connection string format.", exception.Message);
    }

    [TestMethod]
    public void ValidateAndPrompt_WithPromptedRelativePath_ConvertsToAbsolutePath()
    {
        // Arrange
        var options = new Options
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            Container = "testcontainer",
            Path = null
        };

        var relativePath = "TestFolder";
        _mockConsoleService
            .Setup(x => x.Ask("Enter local path: "))
            .Returns(relativePath);

        // Act
        var result = _optionsValidator.ValidateAndPrompt(options);

        // Assert
        Assert.IsTrue(Path.IsPathRooted(result.Path));
        Assert.AreEqual(Path.GetFullPath(relativePath), result.Path);
    }
}