using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace IDE
{
    public class IDEViewModel : INotifyPropertyChanged
    {
        private FileWatcherService _fileWatcher;
        private string _currentProjectPath;

        public IDEViewModel()
        {
            FileTree = new ObservableCollection<FileTreeItem>();
            _fileWatcher = new FileWatcherService();
            _fileWatcher.FileSystemChanged += OnFileSystemChanged;
        }

        public ObservableCollection<FileTreeItem> FileTree { get; }
        public string CurrentProjectPath => _currentProjectPath;

        public void OpenProject(string folderPath)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    throw new DirectoryNotFoundException($"Папка не найдена: {folderPath}");
                }

                _currentProjectPath = folderPath;
                FileTree.Clear();
                LoadFileTree(folderPath);

                _fileWatcher.StartWatching(folderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия проекта: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void LoadFileTree(string rootPath)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    FileTree.Clear();

                    var rootItem = CreateFileTreeItem(rootPath, Path.GetFileName(rootPath), true);
                    FileTree.Add(rootItem);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки дерева файлов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private FileTreeItem CreateFileTreeItem(string path, string name, bool isDirectory)
        {
            var item = new FileTreeItem
            {
                Name = name,
                FullPath = path,
                IsDirectory = isDirectory,
                Icon = isDirectory ? "📁" : GetFileIcon(path)
            };

            if (isDirectory)
            {
                try
                {
                    var directories = Directory.GetDirectories(path);
                    foreach (var dir in directories)
                    {
                        item.Children.Add(CreateFileTreeItem(dir, Path.GetFileName(dir), true));
                    }

                    var files = Directory.GetFiles(path);
                    foreach (var file in files)
                    {
                        item.Children.Add(CreateFileTreeItem(file, Path.GetFileName(file), false));
                    }
                }
                catch (UnauthorizedAccessException)
                {

                }
                catch (Exception ex)
                {

                }
            }

            return item;
        }

        private void OnFileSystemChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentProjectPath) && Directory.Exists(_currentProjectPath))
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        LoadFileTree(_currentProjectPath);
                    }
                    catch
                    {

                    }
                });
            }
        }

        public void RefreshFileTree()
        {
            if (!string.IsNullOrEmpty(_currentProjectPath) && Directory.Exists(_currentProjectPath))
            {
                LoadFileTree(_currentProjectPath);
            }
        }

        private string GetFileIcon(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();

            return extension switch
            {
                ".c" => "🔷",
                ".cpp" => "🔷",
                ".h" => "🔷",
                ".py" => "🐍",
                ".rs" => "🦀",
                ".js" => "📜",
                ".ts" => "📜",
                ".cs" => "C#",
                ".java" => "☕",
                ".txt" => "📄",
                ".md" => "📝",
                ".xml" => "📋",
                ".json" => "📋",
                ".html" => "🌐",
                ".css" => "🎨",
                ".php" => "🐘",
                ".rb" => "💎",
                ".go" => "🐹",
                ".swift" => "🐦",
                ".kt" => "🅚",
                ".dart" => "🎯",
                ".sql" => "🗃️",
                ".exe" => "⚙️",
                ".dll" => "🔧",
                ".zip" => "🗜️",
                ".rar" => "🗜️",
                ".7z" => "🗜️",
                ".pdf" => "📕",
                ".doc" => "📘",
                ".docx" => "📘",
                ".xls" => "📗",
                ".xlsx" => "📗",
                ".ppt" => "📙",
                ".pptx" => "📙",
                ".png" => "🖼️",
                ".jpg" => "🖼️",
                ".jpeg" => "🖼️",
                ".gif" => "🖼️",
                ".bmp" => "🖼️",
                ".ico" => "🖼️",
                ".svg" => "🖼️",
                ".mp3" => "🎵",
                ".wav" => "🎵",
                ".mp4" => "🎬",
                ".avi" => "🎬",
                ".mkv" => "🎬",
                _ => "📄"
            };
        }

        public void Cleanup()
        {
            try
            {
                _fileWatcher.StopWatching();
                FileTree.Clear();
                _currentProjectPath = null;
            }
            catch
            {

            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}