using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IDE
{
    public partial class MainWindow : Window
    {
        private IDEViewModel _viewModel;
        private bool _isClosing;
        private string _currentFilePath = null;
        private string _originalContent = "";
        private Process _terminalProcess;
        private StreamWriter _terminalInput;
        private CancellationTokenSource _terminalCts;
        private readonly object _terminalLock = new object();
        private bool _isTerminalInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            try
            {
                this.Icon = new BitmapImage(
                    new Uri("pack://application:,,,/icon.ico", UriKind.Absolute));
            }
            catch
            {
                this.Icon = null;
                
            }

            _viewModel = new IDEViewModel();

            Loaded += MainWindow_Loaded;
            Closing += Window_Closing;

            SetupKeyBindings();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetupKeyBindings();
            InitializeTerminal();
        }

        private void SetupKeyBindings()
        {
            var saveCommand = new RoutedCommand();
            saveCommand.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control));
            CommandBindings.Add(new CommandBinding(saveCommand, SaveFile_Click));

            var openCommand = new RoutedCommand();
            openCommand.InputGestures.Add(new KeyGesture(Key.O, ModifierKeys.Control));
            CommandBindings.Add(new CommandBinding(openCommand, OpenFile_Click));

            var newCommand = new RoutedCommand();
            newCommand.InputGestures.Add(new KeyGesture(Key.N, ModifierKeys.Control));
            CommandBindings.Add(new CommandBinding(newCommand, NewFileWithDialog_Click));

            var newEmptyCommand = new RoutedCommand();
            newEmptyCommand.InputGestures.Add(new KeyGesture(Key.N, ModifierKeys.Control | ModifierKeys.Shift));
            CommandBindings.Add(new CommandBinding(newEmptyCommand, NewFile_Click));

            var undoCommand = new RoutedCommand();
            undoCommand.InputGestures.Add(new KeyGesture(Key.Z, ModifierKeys.Control));
            CommandBindings.Add(new CommandBinding(undoCommand, (s, e) =>
            {
                if (MainEditor.CanUndo)
                    MainEditor.Undo();
            }));

            var selectAllCommand = new RoutedCommand();
            selectAllCommand.InputGestures.Add(new KeyGesture(Key.A, ModifierKeys.Control));
            CommandBindings.Add(new CommandBinding(selectAllCommand, (s, e) => MainEditor.SelectAll()));

            var saveAsCommand = new RoutedCommand();
            saveAsCommand.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift));
            CommandBindings.Add(new CommandBinding(saveAsCommand, SaveAsFile_Click));

            var compileCommand = new RoutedCommand();
            compileCommand.InputGestures.Add(new KeyGesture(Key.F5, ModifierKeys.Control));
            CommandBindings.Add(new CommandBinding(compileCommand, CompileC_Click));

            var executeCommand = new RoutedCommand();
            executeCommand.InputGestures.Add(new KeyGesture(Key.Enter, ModifierKeys.Control));
            CommandBindings.Add(new CommandBinding(executeCommand, ExecuteCommand_Click));

            var refreshCommand = new RoutedCommand();
            refreshCommand.InputGestures.Add(new KeyGesture(Key.F5));
            CommandBindings.Add(new CommandBinding(refreshCommand, RefreshProject_Click));
        }

        #region Терминал

        private void InitializeTerminal()
        {
            lock (_terminalLock)
            {
                try
                {
                    StopTerminal();

                    _terminalCts = new CancellationTokenSource();

                    Encoding encoding;
                    try
                    {
                        encoding = Encoding.GetEncoding(866);
                    }
                    catch
                    {
                        encoding = Encoding.GetEncoding(437);
                    }

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = encoding,
                        StandardErrorEncoding = encoding,
                        WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    };

                    _terminalProcess = new Process
                    {
                        StartInfo = startInfo,
                        EnableRaisingEvents = true
                    };

                    _terminalProcess.OutputDataReceived += TerminalProcess_OutputDataReceived;
                    _terminalProcess.ErrorDataReceived += TerminalProcess_ErrorDataReceived;
                    _terminalProcess.Exited += TerminalProcess_Exited;

                    _terminalProcess.Start();
                    _terminalInput = new StreamWriter(_terminalProcess.StandardInput.BaseStream, encoding);

                    _terminalProcess.BeginOutputReadLine();
                    _terminalProcess.BeginErrorReadLine();

                    _terminalInput.WriteLine("@echo off");
                    _terminalInput.WriteLine("cls");
                    _terminalInput.Flush();

                    Dispatcher.Invoke(() =>
                    {
                        TerminalOutput.Document.Blocks.Clear();
                        AppendTerminalOutput("Терминал инициализирован. Введите команду...\n", Brushes.LightGreen);
                        _isTerminalInitialized = true;
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        AppendTerminalOutput($"Ошибка инициализации терминала: {ex.Message}\n", Brushes.Red);
                        _isTerminalInitialized = false;
                    });
                }
            }
        }

        private void TerminalProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Dispatcher.Invoke(() =>
                {
                    AppendTerminalOutput(e.Data + "\n", Brushes.White);
                });
            }
        }

        private void TerminalProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Dispatcher.Invoke(() =>
                {
                    AppendTerminalOutput("ОШИБКА: " + e.Data + "\n", Brushes.Red);
                });
            }
        }

        private void TerminalProcess_Exited(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AppendTerminalOutput("\n[Терминал завершен]\n", Brushes.Yellow);
                _isTerminalInitialized = false;
            });
        }

        private void AppendTerminalOutput(string text, Brush color)
        {
            if (string.IsNullOrEmpty(text)) return;

            Dispatcher.Invoke(() =>
            {
                try
                {
                    var paragraph = new Paragraph();
                    paragraph.Foreground = color;
                    paragraph.FontFamily = new FontFamily("Consolas");
                    paragraph.FontSize = 14;
                    paragraph.Inlines.Add(text);

                    TerminalOutput.Document.Blocks.Add(paragraph);

                    TerminalOutput.ScrollToEnd();

                    if (TerminalOutput.Document.Blocks.Count > 500)
                    {
                        while (TerminalOutput.Document.Blocks.Count > 500)
                        {
                            TerminalOutput.Document.Blocks.Remove(TerminalOutput.Document.Blocks.FirstBlock);
                        }
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        TerminalOutput.AppendText(text);
                        TerminalOutput.ScrollToEnd();
                    }
                    catch { }
                }
            });
        }

        private void ClearTerminal_Click(object sender, RoutedEventArgs e)
        {
            TerminalOutput.Document.Blocks.Clear();
            AppendTerminalOutput("[Терминал очищен]\n", Brushes.Yellow);
        }

        private void RestartTerminal_Click(object sender, RoutedEventArgs e)
        {
            InitializeTerminal();
        }

        private void ExecuteCommand_Click(object sender, RoutedEventArgs e)
        {
            ExecuteTerminalCommand();
        }

        private void CommandInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || CommandInput.Text.Trim().Length > 0)
                {
                    ExecuteTerminalCommand();
                    e.Handled = true;
                }
            }
        }

        private void ExecuteTerminalCommand()
        {
            string command = CommandInput.Text.Trim();
            if (string.IsNullOrEmpty(command)) return;

            AppendTerminalOutput("$ " + command + "\n", Brushes.LimeGreen);

            lock (_terminalLock)
            {
                try
                {
                    if (_terminalProcess != null && !_terminalProcess.HasExited && _terminalInput != null)
                    {
                        _terminalInput.WriteLine(command);
                        _terminalInput.Flush();
                    }
                    else
                    {
                        AppendTerminalOutput("Терминал не активен. Попробуйте перезапустить.\n", Brushes.Red);
                    }
                }
                catch (Exception ex)
                {
                    AppendTerminalOutput($"Ошибка выполнения команды: {ex.Message}\n", Brushes.Red);
                }
            }

            CommandInput.Clear();
            CommandInput.Focus();
        }

        private void CompileC_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath) || !_currentFilePath.EndsWith(".c"))
            {
                MessageBox.Show("Пожалуйста, откройте C файл для компиляции.",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveCurrentFile();

            string fileName = Path.GetFileNameWithoutExtension(_currentFilePath);
            string directory = Path.GetDirectoryName(_currentFilePath);
            string outputFile = Path.Combine(directory, $"{fileName}.exe");

            string compileCommand = $"gcc \"{_currentFilePath}\" -o \"{outputFile}\"";

            AppendTerminalOutput("$ " + compileCommand + "\n", Brushes.LimeGreen);

            lock (_terminalLock)
            {
                try
                {
                    if (_terminalProcess != null && !_terminalProcess.HasExited && _terminalInput != null)
                    {
                        _terminalInput.WriteLine(compileCommand);
                        _terminalInput.Flush();
                    }
                    else
                    {
                        AppendTerminalOutput("Терминал не активен. Попробуйте перезапустить.\n", Brushes.Red);
                    }
                }
                catch (Exception ex)
                {
                    AppendTerminalOutput($"Ошибка компиляции: {ex.Message}\n", Brushes.Red);
                }
            }
        }

        private void StopTerminal()
        {
            lock (_terminalLock)
            {
                try
                {
                    if (_terminalProcess != null)
                    {
                        if (!_terminalProcess.HasExited)
                        {
                            try
                            {
                                _terminalProcess.Kill();
                            }
                            catch { }
                        }

                        _terminalProcess.Dispose();
                        _terminalProcess = null;
                    }

                    if (_terminalCts != null)
                    {
                        _terminalCts.Cancel();
                        _terminalCts.Dispose();
                        _terminalCts = null;
                    }

                    if (_terminalInput != null)
                    {
                        _terminalInput.Dispose();
                        _terminalInput = null;
                    }
                }
                catch { }

                _isTerminalInitialized = false;
            }
        }

        #endregion

        #region Обработчики событий меню

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            OpenProjectDialog();
        }

        private void OpenProjectDialog()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите папку проекта",
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Папка"
            };

            if (dialog.ShowDialog() == true)
            {
                string folderPath = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                {
                    OpenProject(folderPath);
                }
            }
        }

        private void OpenProject(string folderPath)
        {
            _viewModel.OpenProject(folderPath);
            LoadFileTree();
            UpdateTitle($"Проект: {Path.GetFileName(folderPath)}");

            if (_isTerminalInitialized)
            {
                string cdCommand = $"cd /d \"{folderPath}\"";
                AppendTerminalOutput("$ " + cdCommand + "\n", Brushes.LimeGreen);

                lock (_terminalLock)
                {
                    if (_terminalProcess != null && !_terminalProcess.HasExited && _terminalInput != null)
                    {
                        _terminalInput.WriteLine(cdCommand);
                        _terminalInput.Flush();
                    }
                }

                AppendTerminalOutput($"Проект открыт: {folderPath}\n", Brushes.Yellow);
            }
        }

        private void RefreshProject_Click(object sender, RoutedEventArgs e)
        {
            RefreshFileTree();
        }

        private void RefreshFileTree()
        {
            if (!string.IsNullOrEmpty(_viewModel.CurrentProjectPath))
            {
                _viewModel.RefreshFileTree();
                LoadFileTree();
                AppendTerminalOutput($"Дерево файлов обновлено\n", Brushes.Yellow);
            }
            else
            {
                MessageBox.Show("Проект не открыт", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void NewFileInProject_Click(object sender, RoutedEventArgs e)
        {
            CreateNewFileInProject();
        }

        private void NewFileWithDialog_Click(object sender, RoutedEventArgs e)
        {
            CreateNewFileWithDialog();
        }

        private void CreateNewFileWithDialog()
        {
            if (!string.IsNullOrEmpty(_viewModel.CurrentProjectPath))
            {
                CreateNewFileInProject();
            }
            else
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Текстовый файл (*.txt)|*.txt|" +
                            "C файл (*.c)|*.c|" +
                            "Python файл (*.py)|*.py|" +
                            "C# файл (*.cs)|*.cs|" +
                            "Java файл (*.java)|*.java|" +
                            "Все файлы (*.*)|*.*",
                    DefaultExt = ".txt",
                    Title = "Создать новый файл"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    string folderPath = Path.GetDirectoryName(saveDialog.FileName);

                    // Создаем файл
                    try
                    {
                        File.WriteAllText(saveDialog.FileName, "");

                        OpenProject(folderPath);

                        OpenFile(saveDialog.FileName);

                        AppendTerminalOutput($"Создан файл в новом проекте: {saveDialog.FileName}\n", Brushes.Cyan);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка создания файла: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void CreateNewFileInProject()
        {
            if (string.IsNullOrEmpty(_viewModel.CurrentProjectPath))
            {
                MessageBox.Show("Сначала откройте проект", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                InitialDirectory = _viewModel.CurrentProjectPath,
                Filter = "Текстовый файл (*.txt)|*.txt|" +
                        "C файл (*.c)|*.c|" +
                        "Python файл (*.py)|*.py|" +
                        "C# файл (*.cs)|*.cs|" +
                        "Java файл (*.java)|*.java|" +
                        "Все файлы (*.*)|*.*",
                DefaultExt = ".txt",
                Title = "Создать новый файл в проекте"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, "");
                    OpenFile(dialog.FileName);

                    RefreshFileTree();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка создания файла: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog();
        }

        private void OpenFileDialog()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Все файлы (*.*)|*.*|" +
                        "Текстовые файлы (*.txt)|*.txt|" +
                        "C файлы (*.c;*.h)|*.c;*.h|" +
                        "Python файлы (*.py)|*.py|" +
                        "C# файлы (*.cs)|*.cs|" +
                        "Java файлы (*.java)|*.java",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                OpenFile(dialog.FileName);
            }
        }

        private void OpenFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"Файл не найден: {filePath}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                string content = File.ReadAllText(filePath);
                MainEditor.Text = content;
                _originalContent = content;
                _currentFilePath = filePath;
                UpdateTitle($"Файл: {Path.GetFileName(filePath)}");
                AppendTerminalOutput($"Открыт файл: {filePath}\n", Brushes.Cyan);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия файла: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            MainEditor.Text = "";
            _originalContent = "";
            _currentFilePath = null;
            UpdateTitle("Новый файл");
            AppendTerminalOutput("Создан новый файл (без сохранения)\n", Brushes.Cyan);
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentFile();
        }

        private void SaveCurrentFile()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                SaveAsFile();
            }
            else
            {
                try
                {
                    File.WriteAllText(_currentFilePath, MainEditor.Text);
                    _originalContent = MainEditor.Text;
                    UpdateTitle($"Файл: {Path.GetFileName(_currentFilePath)}");
                    AppendTerminalOutput($"Файл сохранен: {_currentFilePath}\n", Brushes.LightGreen);

                    if (!string.IsNullOrEmpty(_viewModel.CurrentProjectPath) &&
                        _currentFilePath.StartsWith(_viewModel.CurrentProjectPath))
                    {
                        RefreshFileTree();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveAsFile_Click(object sender, RoutedEventArgs e)
        {
            SaveAsFile();
        }

        private void SaveAsFile()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Текстовый файл (*.txt)|*.txt|" +
                        "C файл (*.c)|*.c|" +
                        "Python файл (*.py)|*.py|" +
                        "C# файл (*.cs)|*.cs|" +
                        "Java файл (*.java)|*.java|" +
                        "Все файлы (*.*)|*.*",
                DefaultExt = ".txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, MainEditor.Text);
                    _currentFilePath = dialog.FileName;
                    _originalContent = MainEditor.Text;
                    UpdateTitle($"Файл: {Path.GetFileName(_currentFilePath)}");
                    AppendTerminalOutput($"Файл сохранен как: {dialog.FileName}\n", Brushes.LightGreen);

                    if (!string.IsNullOrEmpty(_viewModel.CurrentProjectPath) &&
                        dialog.FileName.StartsWith(_viewModel.CurrentProjectPath))
                    {
                        RefreshFileTree();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentFile();
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (MainEditor.CanUndo)
                MainEditor.Undo();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            MainEditor.SelectAll();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region Обработчики редактора

        private void MainEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MainEditor.Text != _originalContent)
            {
                if (!Title.EndsWith(" *"))
                {
                    UpdateTitle(GetCurrentTitle() + " *");
                }
            }
            else
            {
                string currentTitle = Title.Replace(" *", "");
                if (Title != currentTitle)
                {
                    Title = currentTitle;
                }
            }
        }

        private string GetCurrentTitle()
        {
            if (!string.IsNullOrEmpty(_currentFilePath))
                return $"CodeIDE - Файл: {Path.GetFileName(_currentFilePath)}";
            else if (!string.IsNullOrEmpty(MainEditor.Text))
                return "CodeIDE - Новый файл";
            else
                return "CodeIDE";
        }

        #endregion

        #region Обработчики дерева файлов

        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem treeViewItem && treeViewItem.Tag != null)
            {
                string filePath = treeViewItem.Tag.ToString();
                OpenFile(filePath);
            }
        }

        private void LoadFileTree()
        {
            FileTreeView.Items.Clear();

            if (_viewModel?.FileTree == null || _viewModel.FileTree.Count == 0)
                return;

            foreach (var item in _viewModel.FileTree)
            {
                var treeItem = CreateTreeViewItem(item);
                FileTreeView.Items.Add(treeItem);
            }
        }

        private TreeViewItem CreateTreeViewItem(FileTreeItem modelItem)
        {
            var treeViewItem = new TreeViewItem
            {
                Header = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
            {
                new TextBlock
                {
                    Text = modelItem.Icon,
                    Margin = new Thickness(0, 0, 5, 0),
                    Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204))
                },
                new TextBlock
                {
                    Text = modelItem.Name,
                    Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)) 
                }
            }
                },
                Tag = modelItem.IsDirectory ? null : modelItem.FullPath,
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)), 
                Background = Brushes.Transparent
            };

            if (Application.Current.Resources["DarkTreeViewItemStyle"] is Style treeViewItemStyle)
            {
                treeViewItem.Style = treeViewItemStyle;
            }

            if (modelItem.IsDirectory)
            {
                foreach (var child in modelItem.Children)
                {
                    treeViewItem.Items.Add(CreateTreeViewItem(child));
                }
            }

            return treeViewItem;
        }
        #endregion

        #region Вспомогательные методы

        private void UpdateTitle(string message)
        {
            Title = message;
        }

        private bool HasUnsavedChanges()
        {
            return MainEditor.Text != _originalContent;
        }

        #endregion

        #region Закрытие окна

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_isClosing) return;

            if (HasUnsavedChanges())
            {
                var result = MessageBox.Show(
                    "У вас есть несохраненные изменения. Сохранить перед выходом?",
                    "Сохранение",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveCurrentFile();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            StopTerminal();

            _isClosing = true;
        }

        #endregion
    }
}