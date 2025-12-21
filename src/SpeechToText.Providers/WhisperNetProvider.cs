using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.SpeechToText.Core.Configuration;
using Olbrasoft.SpeechToText.Core.Interfaces;
using Olbrasoft.SpeechToText.Core.Models;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Olbrasoft.SpeechToText.Providers;

/// <summary>
/// Whisper.net based speech transcription provider with GPU acceleration support.
/// Uses singleton pattern to load model once and semaphore for thread-safe access.
/// </summary>
public sealed class WhisperNetProvider : ITranscriptionProvider, IDisposable
{
    private readonly ILogger<WhisperNetProvider> _logger;
    private readonly SpeechToTextOptions _options;
    private static WhisperFactory? _whisperFactory;
    private static WhisperProcessor? _processor;
    private static readonly SemaphoreSlim _lock = new(1, 1);
    private static bool _initialized;
    private bool _disposed;

    private const int SampleRate = 16000;

    public string Name => "WhisperNet";

    public WhisperNetProvider(
        ILogger<WhisperNetProvider> logger,
        IOptions<SpeechToTextOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Initializes the Whisper.net factory and processor (singleton, thread-safe).
    /// </summary>
    private void Initialize()
    {
        if (_initialized)
            return;

        _lock.Wait();
        try
        {
            // Double-check after acquiring lock
            if (_initialized)
                return;

            var modelPath = _options.GetFullModelPath();
            _logger.LogInformation("Loading Whisper.net model from: {ModelPath}", modelPath);

            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"Whisper model not found: {modelPath}");
            }

            // Log runtime library order
            _logger.LogInformation("Runtime library order: {Order}",
                string.Join(", ", RuntimeOptions.RuntimeLibraryOrder));

            // Create factory with GPU acceleration
            var options = new WhisperFactoryOptions { UseGpu = _options.UseGpu };
            _whisperFactory = WhisperFactory.FromPath(modelPath, options);

            // Log which library was loaded
            _logger.LogInformation("Loaded runtime library: {Library}",
                RuntimeOptions.LoadedLibrary?.ToString() ?? "Unknown");

            // Create processor with optimized settings
            var language = _options.DefaultLanguage ?? "auto";
            _processor = _whisperFactory.CreateBuilder()
                .WithLanguage(language)
                // Use beam search for better accuracy
                .WithBeamSearchSamplingStrategy()
                .ParentBuilder
                // Lower temperature = more deterministic output
                .WithTemperature(0.0f)
                // CRITICAL: Disable context to prevent hallucinations
                .WithNoContext()
                .Build();

            _initialized = true;
            _logger.LogInformation("Whisper.net initialized successfully (GPU: {Gpu}, Language: {Language})",
                _options.UseGpu, language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Whisper.net");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WhisperNetProvider));

        request.EnsureValid();

        var startTime = DateTime.UtcNow;

        try
        {
            Initialize();

            _logger.LogDebug("Starting transcription (audio size: {Size} bytes)", request.AudioData.Length);

            // Thread-safe access to Whisper processor
            await _lock.WaitAsync(cancellationToken);
            try
            {
                // Strip WAV header if present
                var pcmData = StripWavHeader(request.AudioData);

                // Convert PCM to float32 samples
                var samples = ConvertPcmToFloat32(pcmData);

                var audioDuration = TimeSpan.FromSeconds(samples.Length / (float)SampleRate);
                _logger.LogDebug("Audio samples: {Count} ({Seconds:F1}s)",
                    samples.Length, audioDuration.TotalSeconds);

                // Process with Whisper.net
                var segments = new List<string>();

                await foreach (var segment in _processor!.ProcessAsync(samples, cancellationToken))
                {
                    if (!string.IsNullOrWhiteSpace(segment.Text))
                    {
                        segments.Add(segment.Text.Trim());
                        _logger.LogDebug("Segment: {Start} -> {End}: {Text}",
                            segment.Start, segment.End, segment.Text);
                    }
                }

                var transcription = string.Join(" ", segments);

                if (string.IsNullOrWhiteSpace(transcription))
                {
                    _logger.LogWarning("Transcription result is empty (likely silence)");
                    return TranscriptionResult.Fail("No speech detected", Name, DateTime.UtcNow - startTime);
                }

                var transcriptionTime = DateTime.UtcNow - startTime;
                _logger.LogInformation("Transcription successful: \"{Text}\" ({Time}ms)",
                    transcription, transcriptionTime.TotalMilliseconds);

                return TranscriptionResult.Ok(
                    transcription,
                    Name,
                    transcriptionTime,
                    request.Language ?? _options.DefaultLanguage,
                    audioDuration,
                    confidence: 1.0f);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Transcription cancelled");
            return TranscriptionResult.Fail("Transcription cancelled", Name, DateTime.UtcNow - startTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed");
            return TranscriptionResult.Fail($"Transcription error: {ex.Message}", Name, DateTime.UtcNow - startTime);
        }
    }

    public Task<TranscriptionProviderInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        var modelPath = _options.GetFullModelPath();
        var modelName = Path.GetFileName(modelPath);

        var info = new TranscriptionProviderInfo
        {
            Name = Name,
            IsAvailable = _initialized && File.Exists(modelPath),
            ModelName = modelName,
            GpuEnabled = _options.UseGpu,
            SupportedLanguages = [], // Whisper supports all languages
            AdditionalInfo = _initialized
                ? $"Loaded from: {modelPath}"
                : "Not initialized yet"
        };

        return Task.FromResult(info);
    }

    private static float[] ConvertPcmToFloat32(byte[] pcmData)
    {
        var samples = new float[pcmData.Length / 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = BitConverter.ToInt16(pcmData, i * 2);
            samples[i] = sample / 32768.0f;
        }
        return samples;
    }

    private static byte[] StripWavHeader(byte[] audioData)
    {
        if (audioData.Length > 44 &&
            audioData[0] == 'R' && audioData[1] == 'I' &&
            audioData[2] == 'F' && audioData[3] == 'F')
        {
            var pcmData = new byte[audioData.Length - 44];
            Array.Copy(audioData, 44, pcmData, 0, pcmData.Length);
            return pcmData;
        }
        return audioData;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Note: We don't dispose singleton resources (_whisperFactory, _processor)
        // because they're shared across instances and should live for the app lifetime.
        // They will be disposed when the app shuts down.

        _disposed = true;
        _logger.LogDebug("WhisperNetProvider disposed");
    }
}
