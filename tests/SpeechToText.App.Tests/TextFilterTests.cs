using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.SpeechToText.App;

namespace Olbrasoft.SpeechToText.App.Tests;

public class TextFilterTests
{
    private readonly Mock<ILogger<TextFilter>> _loggerMock;

    public TextFilterTests()
    {
        _loggerMock = new Mock<ILogger<TextFilter>>();
    }

    [Fact]
    public void Constructor_WithNullConfigPath_ShouldDisableFiltering()
    {
        // Act
        var filter = new TextFilter(_loggerMock.Object, null);

        // Assert
        Assert.False(filter.IsEnabled);
        Assert.Equal(0, filter.PatternCount);
    }

    [Fact]
    public void Constructor_WithEmptyConfigPath_ShouldDisableFiltering()
    {
        // Act
        var filter = new TextFilter(_loggerMock.Object, "");

        // Assert
        Assert.False(filter.IsEnabled);
    }

    [Fact]
    public void Constructor_WithWhitespaceConfigPath_ShouldDisableFiltering()
    {
        // Act
        var filter = new TextFilter(_loggerMock.Object, "   ");

        // Assert
        Assert.False(filter.IsEnabled);
    }

    [Fact]
    public void Constructor_WithNonExistentPath_ShouldNotThrow()
    {
        // Arrange
        var nonExistentPath = "/tmp/non_existent_filter_config_12345.json";

        // Act
        var filter = new TextFilter(_loggerMock.Object, nonExistentPath);

        // Assert
        Assert.False(filter.IsEnabled);
    }

    [Fact]
    public void Apply_WithNullText_ShouldReturnEmptyString()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, null);

        // Act
        var result = filter.Apply(null);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Apply_WithEmptyText_ShouldReturnEmptyString()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, null);

        // Act
        var result = filter.Apply("");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Apply_WithWhitespaceText_ShouldReturnEmptyString()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, null);

        // Act
        var result = filter.Apply("   ");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Apply_WithNoFilters_ShouldReturnTrimmedText()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, null);

        // Act
        var result = filter.Apply("  Hello World  ");

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void Apply_ShouldNormalizeMultipleSpaces()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, null);

        // Act
        var result = filter.Apply("Hello    World");

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void IsEnabled_WithNoPatterns_ShouldReturnFalse()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, null);

        // Assert
        Assert.False(filter.IsEnabled);
    }

    [Fact]
    public void PatternCount_WithNoPatterns_ShouldReturnZero()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, null);

        // Assert
        Assert.Equal(0, filter.PatternCount);
    }

    [Fact]
    public void Reload_WithNoConfig_ShouldNotThrow()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, null);

        // Act & Assert - should not throw
        filter.Reload();
    }

    [Fact]
    public void Apply_WithValidText_ShouldPreserveContent()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, null);
        var text = "This is a valid transcription.";

        // Act
        var result = filter.Apply(text);

        // Assert
        Assert.Equal("This is a valid transcription.", result);
    }
}
