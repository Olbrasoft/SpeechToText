using Microsoft.Extensions.Logging;

namespace Olbrasoft.SpeechToText.App;

/// <summary>
/// System tray icon for the SpeechToText desktop application.
/// Provides Start/Stop Dictation and Quit menu items.
/// </summary>
public class TrayIcon : IDisposable
{
    private readonly ILogger<TrayIcon> _logger;
    private readonly DictationService _dictationService;

    private IntPtr _indicator;
    private string _iconsPath = null!;
    private IntPtr _startItem;
    private IntPtr _stopItem;
    private bool _isInitialized;
    private bool _disposed;

    // Icon file names (without path)
    private const string IconIdle = "trigger-speech-to-text";
    private const string IconRecording = "text-to-speech";
    private const string IconOff = "text-to-speech-off";

    // Keep callbacks alive to prevent GC
    private GObject.GCallback? _startCallback;
    private GObject.GCallback? _stopCallback;
    private GObject.GCallback? _quitCallback;

    public TrayIcon(ILogger<TrayIcon> logger, DictationService dictationService)
    {
        _logger = logger;
        _dictationService = dictationService;
    }

    /// <summary>
    /// Initializes GTK and creates the tray indicator.
    /// Must be called from the main thread.
    /// </summary>
    public void Initialize()
    {
        // Initialize GTK
        int argc = 0;
        IntPtr argv = IntPtr.Zero;
        Gtk.gtk_init(ref argc, ref argv);

        // Setup icon paths
        _iconsPath = Path.Combine(AppContext.BaseDirectory, "icons");

        if (!Directory.Exists(_iconsPath))
        {
            _logger.LogWarning("Icons directory not found: {Path}, trying assets/icons", _iconsPath);
            _iconsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "assets", "icons");
        }

        _logger.LogInformation("Icons path: {Path}", _iconsPath);

        // Create app indicator
        _indicator = AppIndicator.app_indicator_new(
            "speech-to-text",
            IconIdle,
            AppIndicator.Category.ApplicationStatus);

        if (_indicator == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create app indicator");
        }

        AppIndicator.app_indicator_set_icon_theme_path(_indicator, _iconsPath);
        AppIndicator.app_indicator_set_title(_indicator, "Speech to Text");

        // Start as ACTIVE (visible)
        AppIndicator.app_indicator_set_status(_indicator, AppIndicator.Status.Active);

        // Create menu
        CreateMenu();

        // Subscribe to dictation service events
        _dictationService.StateChanged += OnDictationStateChanged;

        _isInitialized = true;
        _logger.LogInformation("TrayIcon initialized");
    }

    private void CreateMenu()
    {
        var menu = Gtk.gtk_menu_new();

        // Start Dictation item
        _startItem = Gtk.gtk_menu_item_new_with_label("▶ Start Dictation");
        Gtk.gtk_menu_shell_append(menu, _startItem);

        _startCallback = (widget, data) =>
        {
            _logger.LogInformation("Start Dictation clicked");
            _ = Task.Run(() => _dictationService.StartDictationAsync());
        };
        GObject.g_signal_connect_data(_startItem, "activate", _startCallback, IntPtr.Zero, IntPtr.Zero, 0);

        // Stop Dictation item (initially disabled)
        _stopItem = Gtk.gtk_menu_item_new_with_label("■ Stop Dictation");
        Gtk.gtk_widget_set_sensitive(_stopItem, false);
        Gtk.gtk_menu_shell_append(menu, _stopItem);

        _stopCallback = (widget, data) =>
        {
            _logger.LogInformation("Stop Dictation clicked");
            _ = Task.Run(() => _dictationService.StopDictationAsync());
        };
        GObject.g_signal_connect_data(_stopItem, "activate", _stopCallback, IntPtr.Zero, IntPtr.Zero, 0);

        // Separator
        var separator = Gtk.gtk_separator_menu_item_new();
        Gtk.gtk_menu_shell_append(menu, separator);

        // Quit item
        var quitItem = Gtk.gtk_menu_item_new_with_label("✕ Quit");
        Gtk.gtk_menu_shell_append(menu, quitItem);

        _quitCallback = (widget, data) =>
        {
            _logger.LogInformation("Quit requested from tray menu");
            Gtk.gtk_main_quit();
        };
        GObject.g_signal_connect_data(quitItem, "activate", _quitCallback, IntPtr.Zero, IntPtr.Zero, 0);

        Gtk.gtk_widget_show_all(menu);
        AppIndicator.app_indicator_set_menu(_indicator, menu);
    }

    private void OnDictationStateChanged(object? sender, DictationState state)
    {
        // Schedule UI update on GTK thread
        GLib.g_idle_add(_ =>
        {
            UpdateIcon(state);
            UpdateMenuItems(state);
            return false; // Don't repeat
        }, IntPtr.Zero);
    }

    private void UpdateIcon(DictationState state)
    {
        if (!_isInitialized || _indicator == IntPtr.Zero)
            return;

        var iconPath = state switch
        {
            DictationState.Recording => Path.Combine(_iconsPath, $"{IconRecording}.svg"),
            DictationState.Transcribing => Path.Combine(_iconsPath, $"{IconRecording}.svg"),
            _ => Path.Combine(_iconsPath, $"{IconIdle}.svg")
        };

        AppIndicator.app_indicator_set_icon_full(_indicator, iconPath, state.ToString());
    }

    private void UpdateMenuItems(DictationState state)
    {
        if (!_isInitialized)
            return;

        bool isRecording = state == DictationState.Recording || state == DictationState.Transcribing;

        Gtk.gtk_widget_set_sensitive(_startItem, !isRecording);
        Gtk.gtk_widget_set_sensitive(_stopItem, isRecording);
    }

    /// <summary>
    /// Runs the GTK main loop. This blocks until gtk_main_quit is called.
    /// </summary>
    public void RunMainLoop()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("TrayIcon not initialized");

        _logger.LogInformation("Starting GTK main loop");
        Gtk.gtk_main();
    }

    /// <summary>
    /// Quits the GTK main loop from another thread.
    /// </summary>
    public void QuitMainLoop()
    {
        if (_isInitialized)
        {
            GLib.g_idle_add(_ =>
            {
                Gtk.gtk_main_quit();
                return false;
            }, IntPtr.Zero);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _dictationService.StateChanged -= OnDictationStateChanged;

        _disposed = true;
    }
}
