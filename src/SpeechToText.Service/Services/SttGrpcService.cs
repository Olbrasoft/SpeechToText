using Google.Protobuf;
using Grpc.Core;
using Olbrasoft.SpeechToText.Core.Interfaces;
using Olbrasoft.SpeechToText.Core.Models;

namespace Olbrasoft.SpeechToText.Service.Services;

/// <summary>
/// gRPC service implementation for speech-to-text transcription.
/// </summary>
public sealed class SttGrpcService : SpeechToText.SpeechToTextBase
{
    private readonly ILogger<SttGrpcService> _logger;
    private readonly ITranscriptionProvider _provider;

    public SttGrpcService(
        ILogger<SttGrpcService> logger,
        ITranscriptionProvider provider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<TranscribeResponse> Transcribe(
        TranscribeRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC Transcribe request (audio size: {Size} bytes)", request.Audio.Length);

        try
        {
            var transcriptionRequest = new TranscriptionRequest
            {
                AudioData = request.Audio.ToByteArray(),
                Language = string.IsNullOrWhiteSpace(request.Language) ? null : request.Language,
                ModelName = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model
            };

            var result = await _provider.TranscribeAsync(transcriptionRequest, context.CancellationToken);

            var response = new TranscribeResponse
            {
                Success = result.Success,
                Text = result.Text ?? string.Empty,
                DurationSeconds = (float)(result.AudioDuration?.TotalSeconds ?? 0),
                ErrorMessage = result.ErrorMessage
            };

            if (!string.IsNullOrWhiteSpace(result.Language))
            {
                response.DetectedLanguage = result.Language;
            }

            _logger.LogInformation("gRPC Transcribe response: Success={Success}, Text=\"{Text}\"",
                response.Success, response.Text);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC Transcribe failed");
            return new TranscribeResponse
            {
                Success = false,
                Text = string.Empty,
                ErrorMessage = $"Internal error: {ex.Message}"
            };
        }
    }

    public override async Task TranscribeStream(
        IAsyncStreamReader<AudioChunk> requestStream,
        IServerStreamWriter<PartialResult> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC TranscribeStream started");

        // Future implementation: streaming transcription
        // For now, just accumulate all chunks and transcribe at the end

        var audioChunks = new List<byte[]>();

        try
        {
            await foreach (var chunk in requestStream.ReadAllAsync(context.CancellationToken))
            {
                audioChunks.Add(chunk.Data.ToByteArray());
                _logger.LogDebug("Received audio chunk: {Size} bytes (final: {IsFinal})",
                    chunk.Data.Length, chunk.IsFinal);
            }

            // Concatenate all chunks
            var totalSize = audioChunks.Sum(c => c.Length);
            var audioData = new byte[totalSize];
            var offset = 0;
            foreach (var chunk in audioChunks)
            {
                Array.Copy(chunk, 0, audioData, offset, chunk.Length);
                offset += chunk.Length;
            }

            _logger.LogInformation("Transcribing accumulated audio ({Size} bytes)", audioData.Length);

            var transcriptionRequest = new TranscriptionRequest
            {
                AudioData = audioData
            };

            var result = await _provider.TranscribeAsync(transcriptionRequest, context.CancellationToken);

            // Send final result
            await responseStream.WriteAsync(new PartialResult
            {
                Text = result.Text ?? string.Empty,
                IsFinal = true,
                Confidence = result.Confidence ?? 1.0f
            });

            _logger.LogInformation("gRPC TranscribeStream completed: \"{Text}\"", result.Text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC TranscribeStream failed");
            await responseStream.WriteAsync(new PartialResult
            {
                Text = $"Error: {ex.Message}",
                IsFinal = true,
                Confidence = 0.0f
            });
        }
    }
}
