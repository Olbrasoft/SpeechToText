# SpeechToText

Linux push-to-talk dictation. CapsLock triggers audio capture, Whisper transcribes, xdotool/dotool types result.

## Stack

.NET 10, ASP.NET Core, SignalR, Whisper.net (CUDA), ALSA, evdev, D-Bus

## Structure

| Path | Purpose |
|------|---------|
| `src/SpeechToText.Core/` | Interfaces, models (platform-agnostic) |
| `src/SpeechToText/` | Linux implementations |
| `src/SpeechToText.App/` | Desktop app with tray icon |
| `src/SpeechToText.Service/` | ASP.NET Core service + SignalR |
| `tests/SpeechToText.Tests/` | Main library tests |
| `tests/SpeechToText.Core.Tests/` | Core tests |
| `tests/SpeechToText.App.Tests/` | App tests (39 tests) |
| `tests/SpeechToText.Service.Tests/` | Service tests |

**Note:** Project names are `SpeechToText.*`, namespaces are `Olbrasoft.SpeechToText.*`.

## Key Files

| File | Purpose |
|------|---------|
| `Core/Interfaces/IAudioRecorder.cs` | Audio recording contract |
| `Core/Interfaces/IKeyboardMonitor.cs` | Keyboard events (read-only, ISP) |
| `Core/Interfaces/IKeySimulator.cs` | Key simulation (write-only, ISP) |
| `Core/Interfaces/ISpeechTranscriber.cs` | Whisper transcription |
| `Core/Interfaces/ITextTyper.cs` | Text input simulation |
| `Core/Models/AudioDataEventArgs.cs` | Audio data event |
| `Core/Models/KeyCode.cs` | Key codes enum |
| `Core/Models/KeyEventArgs.cs` | Keyboard event args |
| `Core/Models/TranscriptionResult.cs` | Transcription result |
| `AlsaAudioRecorder.cs` | ALSA via arecord process |
| `EvdevKeyboardMonitor.cs` | Linux evdev keyboard |
| `UinputKeySimulator.cs` | Linux uinput key sim |
| `Speech/WhisperNetTranscriber.cs` | Whisper.net GPU |
| `TextInput/XdotoolTextTyper.cs` | X11 text input |
| `TextInput/DotoolTextTyper.cs` | Wayland text input |
| `App/DictationService.cs` | Main orchestrator |
| `App/DBusTrayIcon.cs` | D-Bus StatusNotifierItem |
| `Service/DictationWorker.cs` | BackgroundService |
| `Service/Hubs/PttHub.cs` | SignalR hub |

## Commands

```bash
dotnet build                                    # Build all
dotnet test                                     # Run 39 tests
dotnet run --project src/SpeechToText.Service   # Run service
```

## Config

`src/SpeechToText.Service/appsettings.json`:

```json
{
  "PushToTalkDictation": {
    "KeyboardDevice": "/dev/input/by-id/...",
    "TriggerKey": "CapsLock",
    "GgmlModelPath": "/path/to/ggml-large-v3.bin",
    "WhisperLanguage": "cs"
  }
}
```

## Data Flow

```
CapsLock ON -> EvdevKeyboardMonitor.KeyPressed
           -> DictationWorker/DictationService
           -> AlsaAudioRecorder.StartRecordingAsync()
           -> DBusTrayIcon animation starts

CapsLock OFF -> EvdevKeyboardMonitor.KeyReleased
            -> AlsaAudioRecorder.StopRecordingAsync()
            -> WhisperNetTranscriber.TranscribeAsync(audioData)
            -> TextFilter (optional post-processing)
            -> XdotoolTextTyper.TypeTextAsync(text)
            -> PttHub.TranscriptionComplete (SignalR)
```

## Patterns

| Pattern | Usage |
|---------|-------|
| Clean Architecture | Core/Infrastructure/Presentation layers |
| ISP | IKeyboardMonitor (read) vs IKeySimulator (write) |
| Strategy | TextTyperFactory selects X11/Wayland typer |
| DI | Microsoft.Extensions.DependencyInjection |
| IAsyncDisposable | IAudioRecorder, DictationService |

## Dependencies

| Package | Purpose |
|---------|---------|
| Whisper.net | Speech recognition |
| Whisper.net.Runtime.Cuda.Linux | GPU acceleration |
| NAudio | Audio fallback |
| SkiaSharp | SVG icon rendering |
| Tmds.DBus | D-Bus communication |

## Gotchas

- Keyboard device path varies per machine, check `/dev/input/by-id/`
- Needs root or input group for evdev access
- CUDA optional, falls back to CPU
- X11 uses xdotool, Wayland uses dotool
- Single instance enforced via file lock `/tmp/push-to-talk-dictation.lock`
- GTK main loop required for tray icons

## API

SignalR hub at `/hubs/ptt`:
- `RecordingStarted` - recording began
- `RecordingStopped` - recording ended
- `TranscriptionComplete(text)` - result ready
- `Error(message)` - error occurred

## Tests

39 tests in `SpeechToText.App.Tests`:
- `DictationOptionsTests` (22) - config parsing
- `DictationServiceTests` (10) - service behavior
- `SingleInstanceLockTests` (7) - lock mechanism
