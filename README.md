# SpeechToText

[![Build](https://img.shields.io/badge/build-passing-green)](https://github.com/Olbrasoft/SpeechToText)

Speech-to-text functionality for Linux voice assistant. Provides speech recognition and transcription services with API/SignalR control.

## Getting Started

### Prerequisites

- .NET 10 SDK
- Linux (tested on Debian/Ubuntu)
- libevdev (for keyboard input simulation)
- Whisper.net for speech recognition

### Installation

```bash
git clone https://github.com/Olbrasoft/SpeechToText.git
cd SpeechToText
dotnet build
```

### Running Tests

```bash
dotnet test
```

## Project Structure

```
SpeechToText/
├── src/
│   ├── SpeechToText.Core/           # Core logic and interfaces
│   ├── SpeechToText.Linux/          # Linux-specific implementations
│   ├── SpeechToText.App/            # Desktop application
│   └── SpeechToText.Service/        # Background service
├── tests/
│   ├── SpeechToText.Core.Tests/
│   ├── SpeechToText.Linux.Tests/
│   ├── SpeechToText.App.Tests/
│   └── SpeechToText.Service.Tests/
├── assets/                          # Icons and resources
├── data/                            # Desktop/metainfo files
├── deploy/                          # Deployment scripts
├── .github/workflows/               # CI/CD
└── SpeechToText.sln
```

## Architecture

- **SOLID principles** throughout
- Clean separation between Core, Linux platform, App and Service layers
- API/SignalR interface for remote control
- Whisper.net integration for speech recognition

## API Endpoints

- `/api/recording/start` - Start recording
- `/api/recording/stop` - Stop recording and transcribe
- `/api/recording/toggle` - Toggle recording state
- SignalR Hub at `/dictationHub` - Real-time state updates

## License

MIT License - see [LICENSE](LICENSE) file.
