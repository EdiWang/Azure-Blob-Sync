using Edi.AzureBlobSync.Interfaces;
using Edi.AzureBlobSync.Services;
using Moq;
using Xunit;

namespace Edi.AzureBlobSync.Tests.Services;

public class OptionsValidatorTests : IDisposable
{
    private readonly Mock<IConsoleService> _mockConsoleService;
    private readonly OptionsValidator _optionsValidator;

    public OptionsValidatorTests()
    {
        _mockConsoleService = new Mock<IConsoleService>();
        _optionsValidator = new OptionsValidator(_mockConsoleService.Object);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
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
        Assert.Equal(options.ConnectionString, result.ConnectionString);
        Assert.Equal(options.Container, result.Container);
        Assert.Equal(options.Path, result.Path);
        _mockConsoleService.Verify(x => x.Ask(It.IsAny<string>()), Times.Never);
    }

    [Fact]
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
        Assert.Equal(expectedConnectionString, result.ConnectionString);
        _mockConsoleService.Verify(x => x.Ask("Enter Azure Storage Account connection string: "), Times.Once);
    }

    [Fact]
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
        Assert.Equal(expectedContainer, result.Container);
        _mockConsoleService.Verify(x => x.Ask("Enter container name: "), Times.Once);
    }

    [Fact]
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
        Assert.Equal(expectedPath, result.Path);
        _mockConsoleService.Verify(x => x.Ask("Enter local path: "), Times.Once);
    }

    [Fact]
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
        Assert.Equal(expectedConnectionString, result.ConnectionString);
        Assert.Equal(expectedContainer, result.Container);
        Assert.Equal(expectedPath, result.Path);
        _mockConsoleService.Verify(x => x.Ask("Enter Azure Storage Account connection string: "), Times.Once);
        _mockConsoleService.Verify(x => x.Ask("Enter container name: "), Times.Once);
        _mockConsoleService.Verify(x => x.Ask("Enter local path: "), Times.Once);
    }

    [Fact]
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
        var exception = Assert.Throws<ArgumentException>(() => _optionsValidator.ValidateAndPrompt(options));
        Assert.Equal("Invalid connection string format.", exception.Message);
    }

    [Fact]
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
        var exception = Assert.Throws<ArgumentException>(() => _optionsValidator.ValidateAndPrompt(options));
        Assert.Equal("Invalid connection string format.", exception.Message);
    }

    [Fact]
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
        var exception = Assert.Throws<ArgumentException>(() => _optionsValidator.ValidateAndPrompt(options));
        Assert.Equal("Invalid connection string format.", exception.Message);
    }

    [Fact]
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
        Assert.True(Path.IsPathRooted(result.Path));
        Assert.Equal(Path.GetFullPath(relativePath), result.Path);
    }

    [Fact]
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
        Assert.Equal(absolutePath, result.Path);
    }

    [Fact]
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
        var exception = Assert.Throws<ArgumentException>(() => _optionsValidator.ValidateAndPrompt(options));
        Assert.Equal("Invalid connection string format.", exception.Message);
    }

    [Fact]
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
        Assert.True(Path.IsPathRooted(result.Path));
        Assert.Equal(Path.GetFullPath(relativePath), result.Path);
    }
}