# SpeechToText

Linux speech-to-text service with push-to-talk dictation using Whisper.

## Features

- **Push-to-Talk Dictation**: Press CapsLock to start recording, release to transcribe and type
- **Whisper Transcription**: Uses Whisper.net for high-quality speech recognition
- **Multiple Triggers**: Supports keyboard (CapsLock), Bluetooth mouse, and USB mouse triggers
- **Tray Icon**: System tray integration with status indication
- **Text Typing**: Automatically types transcribed text using xdotool (X11) or dotool (Wayland)
- **SignalR Hub**: Real-time notifications for recording/transcription status

## Requirements

- .NET 10.0
- Linux with ALSA audio support
- Whisper model file (e.g., `ggml-medium.bin`)
- xdotool (X11) or dotool (Wayland) for text input

## Building

```bash
dotnet build
```

## Configuration

Edit `appsettings.json`:

```json
{
  "PushToTalkDictation": {
    "TriggerKey": "CapsLock",
    "GgmlModelPath": "/path/to/ggml-medium.bin",
    "WhisperLanguage": "cs"
  }
}
```

## Running

```bash
dotnet run --project src/Olbrasoft.SpeechToText.Service
```

## Project Structure

- `Olbrasoft.SpeechToText` - Core library with audio recording, keyboard monitoring, and Whisper transcription
- `Olbrasoft.SpeechToText.Service` - ASP.NET Core service with tray icon and SignalR hub

## License

MIT
