using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.SpeechToText.App;

/// <summary>
/// Service for playing typing sound during transcription.
/// Uses pw-play (PipeWire) or paplay (PulseAudio) to play audio.
/// </summary>
public class TypingSoundPlayer : IDisposable
{
    private readonly ILogger<TypingSoundPlayer> _logger;
    private readonly string? _soundFilePath;
    private Process? _playProcess;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private readonly object _lock = new();
    private bool _isPlaying;
    private bool _disposed;
    private string? _cachedPlayer;

    public TypingSoundPlayer(ILogger<TypingSoundPlayer> logger, string? soundFilePath = null)
    {
        _logger = logger;
        _soundFilePath = soundFilePath;

        if (string.IsNullOrWhiteSpace(_soundFilePath))
        {
            _logger.LogInformation("Transcription sound disabled (no path configured)");
        }
        else if (!File.Exists(_soundFilePath))
        {
            _logger.LogWarning("Transcription sound file not found: {Path}", _soundFilePath);
            _soundFilePath = null;
        }
        else
        {
            _logger.LogInformation("Transcription sound file: {Path}", _soundFilePath);
        }
    }

    /// <summary>
    /// Gets whether sound playback is enabled.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(_soundFilePath);

    /// <summary>
    /// Starts playing the typing sound in a loop.
    /// </summary>
    public void StartLoop()
    {
        lock (_lock)
        {
            if (_isPlaying || _disposed || !IsEnabled)
                return;

            _isPlaying = true;
            _loopCts = new CancellationTokenSource();
            _loopTask = PlayLoopAsync(_loopCts.Token);

            _logger.LogDebug("Transcription sound loop started");
        }
    }

    /// <summary>
    /// Stops the typing sound loop.
    /// </summary>
    public void StopLoop()
    {
        lock (_lock)
        {
            if (!_isPlaying)
                return;

            _isPlaying = false;
            _loopCts?.Cancel();

            // Kill any running play process
            try
            {
                if (_playProcess != null && !_playProcess.HasExited)
                {
                    _playProcess.Kill();
                    _playProcess.Dispose();
                    _playProcess = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error stopping play process");
            }

            _logger.LogDebug("Transcription sound loop stopped");
        }
    }

    private async Task PlayLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PlayOnceAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error in play loop");
                // Small delay before retry
                try
                {
                    await Task.Delay(100, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task PlayOnceAsync(CancellationToken cancellationToken)
    {
        var player = await GetAvailablePlayerAsync();

        if (string.IsNullOrEmpty(player))
        {
            _logger.LogWarning("No audio player available (tried pw-play, paplay)");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = player,
            Arguments = $"\"{_soundFilePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        lock (_lock)
        {
            if (!_isPlaying)
                return;

            _playProcess = Process.Start(startInfo);
        }

        if (_playProcess != null)
        {
            try
            {
                await _playProcess.WaitForExitAsync(cancellationToken);
            }
            finally
            {
                lock (_lock)
                {
                    _playProcess?.Dispose();
                    _playProcess = null;
                }
            }
        }
    }

    private async Task<string?> GetAvailablePlayerAsync()
    {
        // Return cached player if already found
        if (_cachedPlayer != null)
            return _cachedPlayer;

        // Check for pw-play (PipeWire)
        if (await IsCommandAvailableAsync("pw-play"))
        {
            _cachedPlayer = "pw-play";
            return _cachedPlayer;
        }

        // Check for paplay (PulseAudio)
        if (await IsCommandAvailableAsync("paplay"))
        {
            _cachedPlayer = "paplay";
            return _cachedPlayer;
        }

        return null;
    }

    private static async Task<bool> IsCommandAvailableAsync(string command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
        }
        catch
        {
            // Ignore
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopLoop();
        _loopCts?.Dispose();

        GC.SuppressFinalize(this);
    }
}
