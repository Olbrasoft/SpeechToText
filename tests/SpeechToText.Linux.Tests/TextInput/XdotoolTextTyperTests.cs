using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.SpeechToText.TextInput;
using Olbrasoft.Testing.Xunit.Attributes;

namespace Olbrasoft.SpeechToText.Linux.Tests.TextInput;

/// <summary>
/// Tests for XdotoolTextTyper.
/// WARNING: Tests that create real instances are commented out because they can
/// spawn xdotool processes which interfere with GUI and cause unexpected behavior.
/// They should only be run manually in controlled environments.
/// </summary>
public class XdotoolTextTyperTests
{
    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new XdotoolTextTyper(null!));
    }

    // ALL OTHER TESTS ARE COMMENTED OUT - THEY CREATE REAL XDOTOOL INSTANCES
    // WHICH CAN SPAWN PROCESSES AND INTERFERE WITH THE GUI ENVIRONMENT.
    //
    // To run these tests manually:
    // 1. Uncomment the test you want to run
    // 2. Run it in an isolated environment (no active windows you care about)
    // 3. Comment it back after testing
    //
    // [SkipOnCIFact]
    // public void Constructor_WithValidLogger_ShouldNotThrow()
    // {
    //     var loggerMock = new Mock<ILogger<XdotoolTextTyper>>();
    //     var exception = Record.Exception(() => new XdotoolTextTyper(loggerMock.Object));
    //     Assert.Null(exception);
    // }
    //
    // [SkipOnCITheory]
    // [InlineData(0)]
    // [InlineData(1)]
    // [InlineData(50)]
    // [InlineData(100)]
    // public void Constructor_WithDelayParameter_ShouldNotThrow(int delay)
    // {
    //     var loggerMock = new Mock<ILogger<XdotoolTextTyper>>();
    //     var exception = Record.Exception(() => new XdotoolTextTyper(loggerMock.Object, delay));
    //     Assert.Null(exception);
    // }
    //
    // [SkipOnCIFact]
    // public async Task TypeTextAsync_WithNullText_ShouldReturnEarly()
    // {
    //     var loggerMock = new Mock<ILogger<XdotoolTextTyper>>();
    //     var typer = new XdotoolTextTyper(loggerMock.Object);
    //     var exception = await Record.ExceptionAsync(() => typer.TypeTextAsync(null!));
    //     Assert.Null(exception);
    // }
    //
    // [SkipOnCIFact]
    // public async Task TypeTextAsync_WithEmptyText_ShouldReturnEarly()
    // {
    //     var loggerMock = new Mock<ILogger<XdotoolTextTyper>>();
    //     var typer = new XdotoolTextTyper(loggerMock.Object);
    //     var exception = await Record.ExceptionAsync(() => typer.TypeTextAsync(string.Empty));
    //     Assert.Null(exception);
    // }
    //
    // [SkipOnCITheory]
    // [InlineData(" ")]
    // [InlineData("  ")]
    // [InlineData("\t")]
    // [InlineData("\n")]
    // [InlineData("   \t\n   ")]
    // public async Task TypeTextAsync_WithWhitespaceText_ShouldReturnEarly(string text)
    // {
    //     var loggerMock = new Mock<ILogger<XdotoolTextTyper>>();
    //     var typer = new XdotoolTextTyper(loggerMock.Object);
    //     var exception = await Record.ExceptionAsync(() => typer.TypeTextAsync(text));
    //     Assert.Null(exception);
    // }
    //
    // [SkipOnCIFact]
    // public void IsAvailable_ShouldReturnBooleanWithoutException()
    // {
    //     var loggerMock = new Mock<ILogger<XdotoolTextTyper>>();
    //     var typer = new XdotoolTextTyper(loggerMock.Object);
    //     var exception = Record.Exception(() => _ = typer.IsAvailable);
    //     Assert.Null(exception);
    // }
    //
    // [SkipOnCIFact]
    // public async Task TypeTextAsync_WhenNotAvailable_ShouldThrowInvalidOperationException()
    // {
    //     var loggerMock = new Mock<ILogger<XdotoolTextTyper>>();
    //     var typer = new XdotoolTextTyper(loggerMock.Object);
    //     if (typer.IsAvailable) return;
    //     await Assert.ThrowsAsync<InvalidOperationException>(() => typer.TypeTextAsync("test text"));
    // }
    //
    // [SkipOnCIFact]
    // public async Task TypeTextAsync_WithCancellationToken_ShouldAcceptToken()
    // {
    //     var loggerMock = new Mock<ILogger<XdotoolTextTyper>>();
    //     var typer = new XdotoolTextTyper(loggerMock.Object);
    //     var cts = new CancellationTokenSource();
    //     await typer.TypeTextAsync(null!, cts.Token);
    // }
}
