using System;
using System.IO;
using System.Threading;

namespace IDE
{
    public class FileWatcherService : IDisposable
    {
        private FileSystemWatcher _watcher;
        private bool _disposed;
        private string _currentPath;
        private Timer _refreshTimer;
        private const int REFRESH_DELAY = 500; 

        public event EventHandler FileSystemChanged;

        public void StartWatching(string path)
        {
            StopWatching();
            _currentPath = path;

            try
            {
                _watcher = new FileSystemWatcher
                {
                    Path = path,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName |
                                 NotifyFilters.DirectoryName |
                                 NotifyFilters.LastWrite |
                                 NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _watcher.Created += OnFileSystemChanged;
                _watcher.Deleted += OnFileSystemChanged;
                _watcher.Changed += OnFileSystemChanged;
                _watcher.Renamed += OnFileSystemChanged;

                _refreshTimer = new Timer(RefreshCallback, null, Timeout.Infinite, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка запуска FileWatcher: {ex.Message}");
            }
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            _refreshTimer?.Change(REFRESH_DELAY, Timeout.Infinite);
        }

        private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
        {
            _refreshTimer?.Change(REFRESH_DELAY, Timeout.Infinite);
        }

        private void RefreshCallback(object state)
        {
            FileSystemChanged?.Invoke(this, EventArgs.Empty);
        }

        public void StopWatching()
        {
            try
            {
                if (_watcher != null)
                {
                    _watcher.Created -= OnFileSystemChanged;
                    _watcher.Deleted -= OnFileSystemChanged;
                    _watcher.Changed -= OnFileSystemChanged;
                    _watcher.Renamed -= OnFileSystemRenamed;

                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                    _watcher = null;
                }

                if (_refreshTimer != null)
                {
                    _refreshTimer.Dispose();
                    _refreshTimer = null;
                }

                _currentPath = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка остановки FileWatcher: {ex.Message}");
            }
        }

        public void ManualRefresh()
        {
            FileSystemChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopWatching();
                _disposed = true;
            }
        }
    }
}