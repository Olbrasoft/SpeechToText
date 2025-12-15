using System.Diagnostics;
using Olbrasoft.SpeechToText.Service.Hubs;
using Olbrasoft.SpeechToText.Service.Services;

namespace Olbrasoft.SpeechToText.Service;

/// <summary>
/// Extension methods for mapping SpeechToText API endpoints.
/// </summary>
public static class EndpointExtensions
{
    /// <summary>
    /// Maps all SpeechToText API endpoints and SignalR hubs.
    /// </summary>
    public static WebApplication MapSpeechToTextEndpoints(this WebApplication app)
    {
        // Map SignalR hub
        app.MapHub<PttHub>("/hubs/ptt");

        // Health check endpoint
        app.MapGet("/", () => Results.Ok(new { service = "Olbrasoft.SpeechToText", status = "running" }));

        // Repeat last transcription endpoint - copies last text to clipboard
        app.MapPost("/api/ptt/repeat", RepeatTranscriptionHandler);

        return app;
    }

    private static async Task<IResult> RepeatTranscriptionHandler(
        ITranscriptionHistory history,
        ILogger<Program> logger)
    {
        var lastText = history.LastText;

        if (string.IsNullOrEmpty(lastText))
        {
            logger.LogWarning("Repeat requested but no transcription history available");
            return Results.NotFound(new { success = false, message = "No transcription history available" });
        }

        try
        {
            // Copy to clipboard using wl-copy
            var wlCopyProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "wl-copy",
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            wlCopyProcess.Start();
            await wlCopyProcess.StandardInput.WriteAsync(lastText);
            wlCopyProcess.StandardInput.Close();
            await wlCopyProcess.WaitForExitAsync();

            if (wlCopyProcess.ExitCode != 0)
            {
                var error = await wlCopyProcess.StandardError.ReadToEndAsync();
                logger.LogError("wl-copy failed with exit code {ExitCode}: {Error}", wlCopyProcess.ExitCode, error);
                return Results.StatusCode(500);
            }

            logger.LogInformation("Repeat: copied last transcription to clipboard ({Length} chars)", lastText.Length);
            return Results.Ok(new { success = true, text = lastText, copiedToClipboard = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to copy text to clipboard");
            return Results.StatusCode(500);
        }
    }
}
