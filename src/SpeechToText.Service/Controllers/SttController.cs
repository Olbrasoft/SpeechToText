using Microsoft.AspNetCore.Mvc;
using Olbrasoft.SpeechToText.Core.Interfaces;
using Olbrasoft.SpeechToText.Core.Models;

namespace Olbrasoft.SpeechToText.Service.Controllers;

/// <summary>
/// REST API controller for speech-to-text transcription.
/// Provides HTTP endpoints for debugging and monitoring alongside the primary gRPC service.
/// Clients should prefer gRPC for production use due to better performance.
/// </summary>
[ApiController]
[Route("api/stt")]
public sealed class SttController : ControllerBase
{
    private readonly ILogger<SttController> _logger;
    private readonly ITranscriptionProvider _provider;

    public SttController(
        ILogger<SttController> logger,
        ITranscriptionProvider provider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>
    /// Transcribes speech from audio data.
    /// </summary>
    [HttpPost("transcribe")]
    [RequestSizeLimit(10_000_000)] // Max 10MB audio
    [ProducesResponseType(typeof(TranscriptionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Transcribe(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("REST Transcribe request");

            // Read binary stream from request body
            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms, ct);
            byte[] audioData = ms.ToArray();

            _logger.LogDebug("Received audio data: {Size} bytes", audioData.Length);

            var request = new TranscriptionRequest
            {
                AudioData = audioData
            };

            var result = await _provider.TranscribeAsync(request, ct);

            var response = new TranscriptionResponse
            {
                Success = result.Success,
                Text = result.Text,
                DetectedLanguage = result.Language,
                DurationSeconds = (float?)result.AudioDuration?.TotalSeconds,
                ErrorMessage = result.ErrorMessage
            };

            _logger.LogInformation("REST Transcribe response: Success={Success}, Text=\"{Text}\"",
                response.Success, response.Text);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REST Transcribe failed");
            return StatusCode(500, new TranscriptionResponse
            {
                Success = false,
                ErrorMessage = $"Internal error: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Gets provider information.
    /// </summary>
    [HttpGet("info")]
    [ProducesResponseType(typeof(TranscriptionProviderInfo), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInfo(CancellationToken ct)
    {
        var info = await _provider.GetInfoAsync(ct);
        return Ok(info);
    }
}

/// <summary>
/// Response model for REST API.
/// </summary>
public sealed class TranscriptionResponse
{
    public bool Success { get; set; }
    public string? Text { get; set; }
    public string? DetectedLanguage { get; set; }
    public float? DurationSeconds { get; set; }
    public string? ErrorMessage { get; set; }
}
