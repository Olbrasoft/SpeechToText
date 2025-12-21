# SpeechToText Microservice

Speech-to-Text microservice powered by Whisper.net with gRPC and REST API endpoints.

## Features

- **gRPC Primary API** - Low-latency binary protocol (2-5ms overhead)
- **REST API Fallback** - HTTP endpoints for debugging and monitoring
- **GPU Acceleration** - CUDA support for faster transcription
- **Singleton Model Loading** - Loads Whisper model once (saves 3GB+ RAM vs per-process loading)
- **Thread-Safe** - Handles concurrent requests with semaphore locking
- **FHS-Compliant** - Uses standard Linux filesystem hierarchy for shared models

## Architecture

```
SpeechToText/
├── src/
│   ├── SpeechToText.Core/          # Interfaces, models, configuration
│   ├── SpeechToText.Providers/     # Whisper.net implementation
│   └── SpeechToText.Service/       # gRPC + REST API service
├── tests/                           # Unit tests (future)
└── deploy/                          # Deployment scripts and systemd files
```

## API Documentation

### gRPC API (Primary)

**Proto Definition:**
```protobuf
service SpeechToText {
  rpc Transcribe (TranscribeRequest) returns (TranscribeResponse);
  rpc TranscribeStream (stream AudioChunk) returns (stream PartialResult);
}
```

**Example (C# client):**
```csharp
var channel = GrpcChannel.ForAddress("http://localhost:5052");
var client = new SpeechToText.SpeechToTextClient(channel);

var request = new TranscribeRequest
{
    Audio = ByteString.CopyFrom(audioData),
    Language = "cs"
};

var response = await client.TranscribeAsync(request);
Console.WriteLine(response.Text);
```

### REST API (Fallback)

**Transcribe:**
```bash
curl -X POST http://localhost:5052/api/stt/transcribe \
  -H "Content-Type: application/octet-stream" \
  --data-binary @audio.wav

# Response:
{
  "success": true,
  "text": "přeložený text",
  "detectedLanguage": "cs",
  "durationSeconds": 2.5
}
```

**Provider Info:**
```bash
curl http://localhost:5052/api/stt/info

# Response:
{
  "name": "WhisperNet",
  "isAvailable": true,
  "modelName": "ggml-medium.bin",
  "gpuEnabled": true,
  "supportedLanguages": [],
  "additionalInfo": "Loaded from: ~/.local/share/whisper-models/ggml-medium.bin"
}
```

**Health Check:**
```bash
curl http://localhost:5052/health
```

## Configuration

**appsettings.json:**
```json
{
  "SpeechToText": {
    "ModelPath": "ggml-medium.bin",
    "DefaultLanguage": "cs",
    "UseGpu": true,
    "MaxConcurrentRequests": 3
  }
}
```

**Model Location (FHS-compliant):**
1. `~/.local/share/whisper-models/` (XDG user directory, preferred)
2. `/usr/share/whisper-models/` (system-wide)
3. `~/apps/asr-models/` (legacy fallback)

## Deployment

### Production Deployment

```bash
# Build and deploy
./deploy/deploy.sh /opt/olbrasoft/speech-to-text

# Install systemd service
sudo cp deploy/systemd/speech-to-text.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable speech-to-text.service
sudo systemctl start speech-to-text.service

# Check status
systemctl status speech-to-text.service
journalctl -u speech-to-text.service -f
```

### Directory Structure (Production)

```
/opt/olbrasoft/speech-to-text/
├── app/                    # Binaries (deployed here)
│   └── SpeechToText.Service
├── config/                 # Configuration
│   └── appsettings.json
└── logs/                   # Runtime logs
```

**Shared resources:**
- Whisper models: `~/.local/share/whisper-models/` (shared with other apps)

## Performance

| Metric | Target |
|--------|--------|
| gRPC latency | 2-5ms overhead |
| REST latency | 10-20ms overhead |
| Transcription time | 1-3s (medium model) |
| Throughput | ~200 req/s (gRPC) |
| Memory usage | 1.5GB (single model loaded) |

## Use Cases

- **VirtualAssistant** - Continuous listening with ContinuousListener
- **PushToTalk** - Dictation on button press
- **Future Projects** - Gesture-based control, real-time transcription

## Development

### Build
```bash
dotnet build
```

### Run Locally
```bash
dotnet run --project src/SpeechToText.Service

# Service listens on http://localhost:5052
```

### Test gRPC
```bash
# Install grpcurl
go install github.com/fullstorydev/grpcurl/cmd/grpcurl@latest

# List services
grpcurl -plaintext localhost:5052 list

# Call Transcribe
grpcurl -plaintext -d @ localhost:5052 speechtotext.SpeechToText/Transcribe <<EOF
{
  "audio": "BASE64_AUDIO_DATA",
  "language": "cs"
}
EOF
```

## Dependencies

- **.NET 10** (`net10.0`)
- **Grpc.AspNetCore** - gRPC server framework
- **Whisper.net 1.8.0** - Whisper.cpp .NET bindings
- **Whisper.net.Runtime.Cuda.Linux** - CUDA GPU acceleration

## License

MIT

## Related Projects

- [TextToSpeech](https://github.com/Olbrasoft/TextToSpeech) - TTS microservice (similar architecture)
- [VirtualAssistant](https://github.com/Olbrasoft/VirtualAssistant) - Voice-controlled assistant
- [PushToTalk](https://github.com/Olbrasoft/PushToTalk) - Dictation tool
