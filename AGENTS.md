# AGENTS.md

Instructions for AI agents working with this repository.

## Project Overview

Speech-to-text functionality for Linux voice assistant. Provides speech recognition and transcription services controlled via API/SignalR interface for gesture-based triggers.

## Build Commands

```bash
dotnet build
dotnet test
dotnet publish -c Release -o ./publish
```

## Code Style

- Follow Microsoft C# naming conventions
- Use xUnit + Moq for testing
- Target .NET 10
- Namespace prefix: `Olbrasoft.SpeechToText`

## Important Paths

- Source: `src/`
  - `SpeechToText.Core/` - Core logic and interfaces
  - `SpeechToText.Linux/` - Linux-specific implementations
  - `SpeechToText.App/` - Desktop application
  - `SpeechToText.Service/` - Background service
- Tests: `tests/`
  - Each source project has its own test project
- Solution: `SpeechToText.sln`

## Architecture

- **SOLID principles** - especially Single Responsibility and Dependency Inversion
- Clean separation: Core (interfaces/models) -> Linux (platform) -> App/Service (UI/hosting)
- API/SignalR for remote control
- Whisper.net for speech recognition

## Testing Requirements

- Test naming: `[Method]_[Scenario]_[Expected]`
- Example: `StartRecording_WhenIdle_StartsRecording`
- Framework: xUnit + Moq

## Secrets

Never commit secrets. Use:
- `dotnet user-secrets` for local development
- GitHub Secrets for CI/CD

## Related Projects

- VirtualAssistant (`~/Olbrasoft/VirtualAssistant/`) - Main voice assistant
- edge-tts-server - Text-to-speech service
