using Microsoft.Extensions.Logging;

namespace Olbrasoft.SpeechToText.Service.Services;

/// <summary>
/// Manages recording mode state including TTS coordination and speech locks.
/// Combines TTS control and speech lock services into a single service.
/// </summary>
public class RecordingModeManager : IRecordingModeManager
{
    private readonly ILogger<RecordingModeManager> _logger;
    private readonly ITtsControlService _ttsControlService;
    private readonly ISpeechLockService _speechLockService;

    public RecordingModeManager(
        ILogger<RecordingModeManager> logger,
        ITtsControlService ttsControlService,
        ISpeechLockService speechLockService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ttsControlService = ttsControlService ?? throw new ArgumentNullException(nameof(ttsControlService));
        _speechLockService = speechLockService ?? throw new ArgumentNullException(nameof(speechLockService));
    }

    /// <inheritdoc />
    public async Task<RecordingModeContext> EnterRecordingModeAsync()
    {
        _logger.LogDebug("Entering recording mode...");

        // Stop any TTS speech immediately (fire-and-forget but don't await)
        _ = _ttsControlService.StopAllSpeechAsync();

        // Save current mute state before forcing mute (to restore after recording)
        var previousMuteState = await _ttsControlService.GetMuteStateAsync();
        _logger.LogDebug("Saved previous mute state: {PreviousMuteState}", previousMuteState);

        // Mute VirtualAssistant (changes tray icon to muted state)
        // Only set if not already muted
        if (previousMuteState != true)
        {
            await _ttsControlService.SetMuteAsync(true);
        }

        // Create speech lock synchronously to prevent TTS from speaking during recording
        _speechLockService.CreateLock("PushToTalk:Recording");

        _logger.LogDebug("Recording mode entered");
        return new RecordingModeContext(previousMuteState);
    }

    /// <inheritdoc />
    public async Task ExitRecordingModeAsync(RecordingModeContext context)
    {
        _logger.LogDebug("Exiting recording mode...");

        // Delete speech lock to allow TTS to speak again
        _speechLockService.ReleaseLock();

        // Restore VirtualAssistant mute state to what it was before recording
        if (context.PreviousMuteState.HasValue)
        {
            _logger.LogDebug("Restoring previous mute state: {PreviousMuteState}", context.PreviousMuteState.Value);
            await _ttsControlService.SetMuteAsync(context.PreviousMuteState.Value);
        }
        else
        {
            // Fallback: if we couldn't get previous state, unmute
            await _ttsControlService.SetMuteAsync(false);
        }

        // Flush any queued TTS messages that accumulated during dictation
        await _ttsControlService.FlushQueueAsync();

        _logger.LogDebug("Recording mode exited");
    }
}
