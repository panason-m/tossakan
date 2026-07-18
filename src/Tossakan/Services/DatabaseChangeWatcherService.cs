namespace Tossakan.Services;

/// <summary>
/// Watches workmanagement.db (and its -wal/-shm/-journal siblings) for changes made outside the
/// currently-loaded pages — e.g. another Tossakan process pointed at the same file (no single-instance
/// enforcement exists), or an external tool/script. Raises a debounced <see cref="DatabaseChanged"/>
/// event on a background thread; pages are responsible for marshalling back to their own
/// DispatcherQueue before touching UI. SQLite writes typically fire several FileSystemWatcher events
/// in quick succession (WAL checkpoint, journal, etc.), so events are coalesced with a short debounce
/// rather than reacting to every one.
/// </summary>
public class DatabaseChangeWatcherService : IDisposable
{
    private const int DebounceMilliseconds = 400;

    private readonly object _lock = new();
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;

    public event EventHandler? DatabaseChanged;

    public void Start()
    {
        lock (_lock)
        {
            if (_watcher is not null) return;

            var watcher = new FileSystemWatcher(AppPaths.DataFolder)
            {
                Filter = "workmanagement.db*",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            };
            watcher.Changed += OnFileEvent;
            watcher.Created += OnFileEvent;
            watcher.Deleted += OnFileEvent;
            watcher.Renamed += OnFileEvent;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                _ => DatabaseChanged?.Invoke(this, EventArgs.Empty),
                null,
                DebounceMilliseconds,
                Timeout.Infinite);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _watcher?.Dispose();
            _debounceTimer?.Dispose();
        }
    }
}
