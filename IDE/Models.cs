using System.Collections.ObjectModel;
using System.ComponentModel;

namespace IDE
{
    public class FileTreeItem : INotifyPropertyChanged
    {
        private string _name;
        private string _fullPath;
        private bool _isDirectory;
        private string _icon;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string FullPath
        {
            get => _fullPath;
            set { _fullPath = value; OnPropertyChanged(nameof(FullPath)); }
        }

        public bool IsDirectory
        {
            get => _isDirectory;
            set { _isDirectory = value; OnPropertyChanged(nameof(IsDirectory)); }
        }

        public string Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(nameof(Icon)); }
        }

        public ObservableCollection<FileTreeItem> Children { get; } = new ObservableCollection<FileTreeItem>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}