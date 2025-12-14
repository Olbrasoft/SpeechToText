using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.SpeechToText;
using Olbrasoft.SpeechToText.App;
using Olbrasoft.SpeechToText.Speech;
using Olbrasoft.SpeechToText.TextInput;

namespace Olbrasoft.SpeechToText.App.Tests;

public class DictationServiceTests : IDisposable
{
    private readonly Mock<ILogger<DictationService>> _loggerMock;
    private readonly Mock<IKeyboardMonitor> _keyboardMonitorMock;
    private readonly Mock<IAudioRecorder> _audioRecorderMock;
    private readonly Mock<ISpeechTranscriber> _transcriberMock;
    private readonly Mock<ITextTyper> _textTyperMock;
    private readonly DictationService _service;

    public DictationServiceTests()
    {
        _loggerMock = new Mock<ILogger<DictationService>>();
        _keyboardMonitorMock = new Mock<IKeyboardMonitor>();
        _audioRecorderMock = new Mock<IAudioRecorder>();
        _transcriberMock = new Mock<ISpeechTranscriber>();
        _textTyperMock = new Mock<ITextTyper>();

        _service = new DictationService(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _audioRecorderMock.Object,
            _transcriberMock.Object,
            _textTyperMock.Object);
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    [Fact]
    public void InitialState_ShouldBeIdle()
    {
        // Assert
        Assert.Equal(DictationState.Idle, _service.State);
    }

    [Fact]
    public async Task StartDictationAsync_WhenIdle_ShouldStartRecording()
    {
        // Arrange
        var stateChanges = new List<DictationState>();
        _service.StateChanged += (_, state) => stateChanges.Add(state);

        _audioRecorderMock.Setup(r => r.StartRecordingAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.StartDictationAsync();

        // Assert
        Assert.Equal(DictationState.Recording, _service.State);
        Assert.Contains(DictationState.Recording, stateChanges);
        _audioRecorderMock.Verify(r => r.StartRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartDictationAsync_WhenNotIdle_ShouldNotStartRecording()
    {
        // Arrange - start first dictation
        _audioRecorderMock.Setup(r => r.StartRecordingAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await _service.StartDictationAsync();

        // Act - try to start again while recording
        await _service.StartDictationAsync();

        // Assert
        _audioRecorderMock.Verify(r => r.StartRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopDictationAsync_WhenNotRecording_ShouldDoNothing()
    {
        // Act
        await _service.StopDictationAsync();

        // Assert
        _audioRecorderMock.Verify(r => r.StopRecordingAsync(), Times.Never);
        Assert.Equal(DictationState.Idle, _service.State);
    }

    [Fact]
    public async Task StopDictationAsync_WithNoAudioData_ShouldReturnToIdle()
    {
        // Arrange
        _audioRecorderMock.Setup(r => r.StartRecordingAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audioRecorderMock.Setup(r => r.StopRecordingAsync())
            .Returns(Task.CompletedTask);
        _audioRecorderMock.Setup(r => r.GetRecordedData())
            .Returns(Array.Empty<byte>());

        await _service.StartDictationAsync();

        // Act
        await _service.StopDictationAsync();

        // Assert
        Assert.Equal(DictationState.Idle, _service.State);
        _transcriberMock.Verify(t => t.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StopDictationAsync_WithAudioData_ShouldTranscribeAndType()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3, 4, 5 };
        var transcriptionResult = new TranscriptionResult("Hello World", 1.0f);

        _audioRecorderMock.Setup(r => r.StartRecordingAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audioRecorderMock.Setup(r => r.StopRecordingAsync())
            .Returns(Task.CompletedTask);
        _audioRecorderMock.Setup(r => r.GetRecordedData())
            .Returns(audioData);
        _transcriberMock.Setup(t => t.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);
        _textTyperMock.Setup(t => t.TypeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.StartDictationAsync();

        // Act
        await _service.StopDictationAsync();

        // Assert
        Assert.Equal(DictationState.Idle, _service.State);
        _transcriberMock.Verify(t => t.TranscribeAsync(audioData, It.IsAny<CancellationToken>()), Times.Once);
        _textTyperMock.Verify(t => t.TypeTextAsync("Hello World", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopDictationAsync_WithFailedTranscription_ShouldNotType()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3 };
        var transcriptionResult = new TranscriptionResult("Transcription failed");

        _audioRecorderMock.Setup(r => r.StartRecordingAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audioRecorderMock.Setup(r => r.StopRecordingAsync())
            .Returns(Task.CompletedTask);
        _audioRecorderMock.Setup(r => r.GetRecordedData())
            .Returns(audioData);
        _transcriberMock.Setup(t => t.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);

        await _service.StartDictationAsync();

        // Act
        await _service.StopDictationAsync();

        // Assert
        Assert.Equal(DictationState.Idle, _service.State);
        _textTyperMock.Verify(t => t.TypeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartMonitoringAsync_ShouldCallKeyboardMonitor()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately to prevent hanging

        _keyboardMonitorMock.Setup(k => k.StartMonitoringAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.StartMonitoringAsync(cts.Token);

        // Assert
        _keyboardMonitorMock.Verify(k => k.StartMonitoringAsync(cts.Token), Times.Once);
    }

    [Fact]
    public async Task StopMonitoringAsync_ShouldCallKeyboardMonitor()
    {
        // Arrange
        _keyboardMonitorMock.Setup(k => k.StopMonitoringAsync())
            .Returns(Task.CompletedTask);

        // Act
        await _service.StopMonitoringAsync();

        // Assert
        _keyboardMonitorMock.Verify(k => k.StopMonitoringAsync(), Times.Once);
    }

    [Fact]
    public async Task StateChanged_ShouldBeRaised_WhenStateChanges()
    {
        // Arrange
        var stateChanges = new List<DictationState>();
        _service.StateChanged += (_, state) => stateChanges.Add(state);

        _audioRecorderMock.Setup(r => r.StartRecordingAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audioRecorderMock.Setup(r => r.StopRecordingAsync())
            .Returns(Task.CompletedTask);
        _audioRecorderMock.Setup(r => r.GetRecordedData())
            .Returns(Array.Empty<byte>());

        // Act
        await _service.StartDictationAsync();
        await _service.StopDictationAsync();

        // Assert
        Assert.Contains(DictationState.Recording, stateChanges);
        Assert.Contains(DictationState.Idle, stateChanges);
    }
}
