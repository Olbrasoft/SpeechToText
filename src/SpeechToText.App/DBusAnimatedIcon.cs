using Microsoft.Extensions.Logging;
using Tmds.DBus.SourceGenerator;
using Tmds.DBus.Protocol;

namespace Olbrasoft.SpeechToText.App;

/// <summary>
/// Separate D-Bus StatusNotifierItem for animated icon during transcription.
/// Shows/hides as a second tray icon to work around GNOME Shell caching.
/// </summary>
public class DBusAnimatedIcon : IDisposable
{
    private static int s_instanceId = 1000; // Different range from main icon

    private readonly ILogger _logger;
    private readonly SvgIconRenderer _iconRenderer;
    private readonly string[] _frameNames;
    private readonly int _intervalMs;

    private Connection? _connection;
    private OrgFreedesktopDBusProxy? _dBus;
    private OrgKdeStatusNotifierWatcherProxy? _statusNotifierWatcher;
    private AnimatedIconHandler? _sniHandler;
    private PathHandler? _pathHandler;
    private IDisposable? _serviceWatchDisposable;

    private string? _sysTrayServiceName;
    private bool _isDisposed;
    private bool _serviceConnected;
    private bool _isVisible;

    // Animation
    private Timer? _animationTimer;
    private int _currentFrameIndex;
    private readonly object _animationLock = new();

    public bool IsActive { get; private set; }

    public DBusAnimatedIcon(ILogger logger, string iconsPath, string[] frameNames, int iconSize = 22, int intervalMs = 150)
    {
        _logger = logger;
        _iconRenderer = new SvgIconRenderer(logger, iconsPath, iconSize);
        _frameNames = frameNames;
        _intervalMs = intervalMs;
    }

    /// <summary>
    /// Initializes the D-Bus connection (but doesn't show the icon yet).
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _connection = new Connection(Address.Session!);
            await _connection.ConnectAsync();

            _dBus = new OrgFreedesktopDBusProxy(_connection, "org.freedesktop.DBus", "/org/freedesktop/DBus");

            // Use standard StatusNotifierItem path - each icon has its own D-Bus connection
            // so there's no conflict with the main icon
            _pathHandler = new PathHandler("/StatusNotifierItem");
            _sniHandler = new AnimatedIconHandler(_connection, _logger);

            _pathHandler.Add(_sniHandler);
            _connection.AddMethodHandler(_pathHandler);

            // Pre-cache all animation frames
            _logger.LogDebug("Loading animation frames from: {IconsPath}", _iconRenderer.IconsPath);
            _iconRenderer.PreCacheIcons(_frameNames);

            // Watch for StatusNotifierWatcher
            await WatchAsync();

            IsActive = true;
            _logger.LogInformation("DBusAnimatedIcon initialized with {CachedCount}/{TotalCount} frames cached",
                _iconRenderer.CacheCount, _frameNames.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize DBusAnimatedIcon");
            IsActive = false;
        }
    }

    private async Task WatchAsync()
    {
        try
        {
            _serviceWatchDisposable = await _dBus!.WatchNameOwnerChangedAsync(
                (exception, change) =>
                {
                    if (exception is null && change.Item1 == "org.kde.StatusNotifierWatcher")
                    {
                        OnNameChange(change.Item1, change.Item3);
                    }
                },
                emitOnCapturedContext: false);

            var nameOwner = await _dBus.GetNameOwnerAsync("org.kde.StatusNotifierWatcher");
            OnNameChange("org.kde.StatusNotifierWatcher", nameOwner);
        }
        catch (DBusException ex) when (ex.ErrorName == "org.freedesktop.DBus.Error.NameHasNoOwner")
        {
            _logger.LogWarning("StatusNotifierWatcher not available for animated icon");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to watch StatusNotifierWatcher");
        }
    }

    private void OnNameChange(string name, string? newOwner)
    {
        if (_isDisposed || _connection is null || name != "org.kde.StatusNotifierWatcher")
            return;

        if (!_serviceConnected && newOwner is not null)
        {
            _serviceConnected = true;
            _statusNotifierWatcher = new OrgKdeStatusNotifierWatcherProxy(_connection, "org.kde.StatusNotifierWatcher", "/StatusNotifierWatcher");
        }
        else if (_serviceConnected && newOwner is null)
        {
            Hide();
            _serviceConnected = false;
        }
    }

    /// <summary>
    /// Shows the animated icon and starts animation.
    /// </summary>
    public async Task ShowAsync()
    {
        if (_isDisposed || !_serviceConnected || _isVisible || _statusNotifierWatcher is null)
        {
            _logger.LogInformation("ShowAsync skipped: disposed={Disposed}, connected={Connected}, visible={Visible}, watcher={HasWatcher}",
                _isDisposed, _serviceConnected, _isVisible, _statusNotifierWatcher is not null);
            return;
        }

        try
        {
            var pid = Environment.ProcessId;
            var tid = Interlocked.Increment(ref s_instanceId);

            // Re-add handler if needed
            if (_sniHandler!.PathHandler is null)
                _pathHandler!.Add(_sniHandler);

            _connection!.RemoveMethodHandler(_pathHandler!.Path);
            _connection.AddMethodHandler(_pathHandler);

            _sysTrayServiceName = $"org.kde.StatusNotifierItem-{pid}-{tid}";

            // Set first frame BEFORE registration so GNOME Shell reads valid pixmap
            _currentFrameIndex = 0;
            SetCurrentFrame();

            await _dBus!.RequestNameAsync(_sysTrayServiceName, 0);
            await _statusNotifierWatcher.RegisterStatusNotifierItemAsync(_sysTrayServiceName);

            _isVisible = true;
            _animationTimer = new Timer(AnimationCallback, null, _intervalMs, _intervalMs);

            _logger.LogInformation("Animated icon shown as {ServiceName}", _sysTrayServiceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show animated icon");
        }
    }

    /// <summary>
    /// Hides the animated icon and stops animation.
    /// </summary>
    public void Hide()
    {
        if (_isDisposed || !_isVisible)
            return;

        lock (_animationLock)
        {
            _animationTimer?.Dispose();
            _animationTimer = null;
        }

        try
        {
            if (_sysTrayServiceName is not null && _dBus is not null)
            {
                _ = _dBus.ReleaseNameAsync(_sysTrayServiceName);
            }

            if (_sniHandler is not null && _pathHandler is not null)
            {
                _pathHandler.Remove(_sniHandler);
                _connection?.RemoveMethodHandler(_pathHandler.Path);
            }

            _isVisible = false;
            _logger.LogDebug("Animated icon hidden");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error hiding animated icon");
        }
    }

    private void AnimationCallback(object? state)
    {
        if (_isDisposed || !_isVisible)
            return;

        lock (_animationLock)
        {
            _currentFrameIndex = (_currentFrameIndex + 1) % _frameNames.Length;
            SetCurrentFrame();
        }
    }

    private void SetCurrentFrame()
    {
        if (_frameNames.Length == 0 || _sniHandler is null)
        {
            _logger.LogDebug("SetCurrentFrame skipped: frames={FrameCount}, handler={HasHandler}",
                _frameNames.Length, _sniHandler is not null);
            return;
        }

        var frameName = _frameNames[_currentFrameIndex];
        var pixmap = _iconRenderer.GetIcon(frameName);
        if (pixmap.HasValue)
        {
            _sniHandler.SetIcon(pixmap.Value);
            _logger.LogDebug("Set frame {FrameIndex}: {FrameName}", _currentFrameIndex, frameName);
        }
        else
        {
            _logger.LogWarning("Frame not in cache: {FrameName} (cache has {CacheCount} items)",
                frameName, _iconRenderer.CacheCount);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        IsActive = false;

        Hide();
        _serviceWatchDisposable?.Dispose();
        _connection?.Dispose();
        _iconRenderer.ClearCache();

        _logger.LogInformation("DBusAnimatedIcon disposed");
    }
}

/// <summary>
/// D-Bus handler for animated icon.
/// </summary>
internal class AnimatedIconHandler : OrgKdeStatusNotifierItemHandler
{
    private readonly ILogger _logger;

    public AnimatedIconHandler(Connection connection, ILogger logger)
    {
        Connection = connection;
        _logger = logger;

        Category = "ApplicationStatus";
        Id = "speech-to-text-animation";
        Title = "Transcribing...";
        Status = "Active";
        IconName = "";
        IconPixmap = Array.Empty<(int, int, byte[])>();
        OverlayIconName = "";
        OverlayIconPixmap = Array.Empty<(int, int, byte[])>();
        AttentionIconName = "";
        AttentionIconPixmap = Array.Empty<(int, int, byte[])>();
        AttentionMovieName = "";
        IconThemePath = "";
        Menu = new ObjectPath("/MenuBar");
        ItemIsMenu = false;
        ToolTip = ("", Array.Empty<(int, int, byte[])>(), "Transcribing...", "");
        WindowId = 0;
    }

    public override Connection Connection { get; }

    protected override ValueTask OnContextMenuAsync(Message message, int x, int y)
        => ValueTask.CompletedTask;

    protected override ValueTask OnActivateAsync(Message message, int x, int y)
        => ValueTask.CompletedTask;

    protected override ValueTask OnSecondaryActivateAsync(Message message, int x, int y)
        => ValueTask.CompletedTask;

    protected override ValueTask OnScrollAsync(Message message, int delta, string orientation)
        => ValueTask.CompletedTask;

    public void SetIcon((int, int, byte[]) dbusPixmap)
    {
        IconPixmap = new[] { dbusPixmap };
        IconName = "";  // Clear icon name to force pixmap usage
        Status = "Active";

        // Emit signals to notify the tray about the change
        EmitNewIcon();
        EmitNewStatus(Status);
    }
}
