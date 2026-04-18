using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using 网易云音乐下载.Commands;
using 网易云音乐下载.Models;
using 网易云音乐下载.Services;
using Win32OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Win32SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using FormsFolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using InputMode = 网易云音乐下载.Models.InputMode;

namespace 网易云音乐下载.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly AudioConverterService _converterService;
        private readonly NeteaseDownloadService _downloadService;
        private readonly MusicPlayerService _musicPlayerService;
        private CancellationTokenSource _conversionCts;
        private CancellationTokenSource _downloadCts;
        private string _playlistsBasePath;
        private readonly string _playlistStorageConfigPath;
        private bool _isSyncingPlaybackPosition;
        private bool _isUserSeeking;

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get { return _selectedTabIndex; }
            set { SetProperty(ref _selectedTabIndex, value); }
        }

        private bool _isPlaylistDetailView;
        public bool IsPlaylistDetailView
        {
            get { return _isPlaylistDetailView; }
            set { SetProperty(ref _isPlaylistDetailView, value); }
        }

        private PlaylistInfo _playlistDetailPlaylist;
        public PlaylistInfo PlaylistDetailPlaylist
        {
            get { return _playlistDetailPlaylist; }
            set { SetProperty(ref _playlistDetailPlaylist, value); }
        }

        #region 歌单相关属性

        public ObservableCollection<PlaylistInfo> Playlists { get; private set; }

        private PlaylistInfo _selectedPlaylist;
        public PlaylistInfo SelectedPlaylist
        {
            get { return _selectedPlaylist; }
            set
            {
                if (SetProperty(ref _selectedPlaylist, value))
                {
                    // 更新所有歌单的选中状态
                    foreach (var playlist in Playlists)
                    {
                        playlist.IsSelected = (playlist == value);
                    }
                    ((RelayCommand)AddSongToPlaylistCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)RemoveSongFromPlaylistCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DeletePlaylistCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)RenamePlaylistCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)SetPlaylistCoverCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)RemovePlaylistCoverCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)OpenPlaylistCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)PreviousSongCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)NextSongCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private PlaylistSongInfo _selectedSong;
        public PlaylistSongInfo SelectedSong
        {
            get { return _selectedSong; }
            set
            {
                if (SetProperty(ref _selectedSong, value))
                {
                    // 当选中歌曲改变时，通知 UI 更新
                    OnPropertyChanged(nameof(SelectedSong));
                    ((RelayCommand)CopySelectedSongInfoCommand)?.RaiseCanExecuteChanged();
                    ((RelayCommand)CloseSelectedSongInfoCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private string _newPlaylistName;
        public string NewPlaylistName
        {
            get { return _newPlaylistName; }
            set
            {
                if (SetProperty(ref _newPlaylistName, value))
                {
                    ((RelayCommand)CreatePlaylistCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private string _playlistStatusText = "准备就绪";
        public string PlaylistStatusText
        {
            get { return _playlistStatusText; }
            set { SetProperty(ref _playlistStatusText, value); }
        }

        private string _playlistsBaseDirectory;
        public string PlaylistsBaseDirectory
        {
            get { return _playlistsBaseDirectory; }
            set
            {
                if (SetProperty(ref _playlistsBaseDirectory, value))
                {
                    _playlistsBasePath = value;
                }
            }
        }

        private string _newPlaylistCoverPath;
        public string NewPlaylistCoverPath
        {
            get { return _newPlaylistCoverPath; }
            set
            {
                SetProperty(ref _newPlaylistCoverPath, value);
            }
        }

        private bool _isAddToPlaylistDialogVisible;
        public bool IsAddToPlaylistDialogVisible
        {
            get { return _isAddToPlaylistDialogVisible; }
            set { SetProperty(ref _isAddToPlaylistDialogVisible, value); }
        }

        private bool _showImportToPlaylistOptions;
        public bool ShowImportToPlaylistOptions
        {
            get { return _showImportToPlaylistOptions; }
            set { SetProperty(ref _showImportToPlaylistOptions, value); }
        }

        private ObservableCollection<string> _convertedFiles;
        public ObservableCollection<string> ConvertedFiles
        {
            get { return _convertedFiles; }
            set { SetProperty(ref _convertedFiles, value); }
        }

        #endregion

        #region 音乐播放相关属性

        private bool _isPlayerVisible;
        public bool IsPlayerVisible
        {
            get { return _isPlayerVisible; }
            set { SetProperty(ref _isPlayerVisible, value); }
        }

        private string _currentPlayingSongName;
        public string CurrentPlayingSongName
        {
            get { return _currentPlayingSongName; }
            set { SetProperty(ref _currentPlayingSongName, value); }
        }

        private string _currentPlayingSongPath;
        public string CurrentPlayingSongPath
        {
            get { return _currentPlayingSongPath; }
            set { SetProperty(ref _currentPlayingSongPath, value); }
        }

        private TimeSpan _currentPosition;
        public TimeSpan CurrentPosition
        {
            get { return _currentPosition; }
            set { SetProperty(ref _currentPosition, value); }
        }

        private double _currentPositionSeconds;
        public double CurrentPositionSeconds
        {
            get { return _currentPositionSeconds; }
            set
            {
                double clampedValue = ClampSeekSeconds(value);
                if (SetProperty(ref _currentPositionSeconds, clampedValue))
                {
                    if (_isUserSeeking)
                    {
                        CurrentPosition = TimeSpan.FromSeconds(clampedValue);
                        return;
                    }

                    if (_isSyncingPlaybackPosition || _musicPlayerService == null)
                    {
                        return;
                    }

                    if (Math.Abs(_musicPlayerService.CurrentPosition.TotalSeconds - clampedValue) > 0.5)
                    {
                        var targetPosition = TimeSpan.FromSeconds(clampedValue);
                        _musicPlayerService.SetPosition(targetPosition);
                        CurrentPosition = targetPosition;
                    }
                }
            }
        }

        private TimeSpan _totalDuration;
        public TimeSpan TotalDuration
        {
            get { return _totalDuration; }
            set
            {
                if (SetProperty(ref _totalDuration, value))
                {
                    TotalDurationSeconds = value.TotalSeconds;
                }
            }
        }

        private double _totalDurationSeconds;
        public double TotalDurationSeconds
        {
            get { return _totalDurationSeconds; }
            set
            {
                if (SetProperty(ref _totalDurationSeconds, value))
                {
                    OnPropertyChanged(nameof(CanSeekCurrentSong));
                }
            }
        }

        public bool CanSeekCurrentSong
        {
            get { return TotalDurationSeconds > 0; }
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get { return _isPlaying; }
            set { SetProperty(ref _isPlaying, value); }
        }

        private PlaylistSongInfo _currentSong;
        public PlaylistSongInfo CurrentSong
        {
            get { return _currentSong; }
            set { SetProperty(ref _currentSong, value); }
        }

        #endregion

        private InputMode _inputMode = InputMode.SingleFile;
        public InputMode InputMode
        {
            get { return _inputMode; }
            set
            {
                if (SetProperty(ref _inputMode, value))
                {
                    InputPath = string.Empty;
                    NcmFiles.Clear();
                    OnPropertyChanged("IsSingleFileMode");
                    OnPropertyChanged("IsBatchMode");
                    OnPropertyChanged("IsMultipleFilesMode");
                }
            }
        }

        public bool IsSingleFileMode
        {
            get { return InputMode == InputMode.SingleFile; }
        }

        public bool IsBatchMode
        {
            get { return InputMode == InputMode.BatchFolder; }
        }

        public bool IsMultipleFilesMode
        {
            get { return InputMode == InputMode.MultipleFiles; }
        }

        private string _inputPath;
        public string InputPath
        {
            get { return _inputPath; }
            set { SetProperty(ref _inputPath, value); }
        }

        private string _outputPath;
        public string OutputPath
        {
            get { return _outputPath; }
            set { SetProperty(ref _outputPath, value); }
        }

        private string _outputFormat = "mp3";
        public string OutputFormat
        {
            get { return _outputFormat; }
            set { SetProperty(ref _outputFormat, value); }
        }

        public ObservableCollection<NcmFileInfo> NcmFiles { get; private set; }

        private int _conversionProgress;
        public int ConversionProgress
        {
            get { return _conversionProgress; }
            set { SetProperty(ref _conversionProgress, value); }
        }

        private string _currentConvertingFile;
        public string CurrentConvertingFile
        {
            get { return _currentConvertingFile; }
            set { SetProperty(ref _currentConvertingFile, value); }
        }

        private string _conversionStatusText = "准备就绪";
        public string ConversionStatusText
        {
            get { return _conversionStatusText; }
            set { SetProperty(ref _conversionStatusText, value); }
        }

        private bool _isConverting;
        public bool IsConverting
        {
            get { return _isConverting; }
            set
            {
                if (SetProperty(ref _isConverting, value))
                {
                    ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)CancelConversionCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private bool _showConversionComplete;
        public bool ShowConversionComplete
        {
            get { return _showConversionComplete; }
            set { SetProperty(ref _showConversionComplete, value); }
        }

        private string _songId;
        public string SongId
        {
            get { return _songId; }
            set
            {
                if (SetProperty(ref _songId, value))
                {
                    ((RelayCommand)StartDownloadCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private string _downloadOutputPath;
        public string DownloadOutputPath
        {
            get { return _downloadOutputPath; }
            set { SetProperty(ref _downloadOutputPath, value); }
        }

        private bool _useSameOutputPath = true;
        public bool UseSameOutputPath
        {
            get { return _useSameOutputPath; }
            set
            {
                if (SetProperty(ref _useSameOutputPath, value))
                {
                    if (value && !string.IsNullOrEmpty(OutputPath))
                    {
                        DownloadOutputPath = OutputPath;
                    }
                }
            }
        }

        public ObservableCollection<DownloadTaskInfo> DownloadTasks { get; private set; }

        private int _downloadProgress;
        public int DownloadProgress
        {
            get { return _downloadProgress; }
            set { SetProperty(ref _downloadProgress, value); }
        }

        private string _downloadStatusText = "准备就绪";
        public string DownloadStatusText
        {
            get { return _downloadStatusText; }
            set { SetProperty(ref _downloadStatusText, value); }
        }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get { return _isDownloading; }
            set
            {
                if (SetProperty(ref _isDownloading, value))
                {
                    ((RelayCommand)StartDownloadCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)CancelDownloadCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private bool _showDownloadComplete;
        public bool ShowDownloadComplete
        {
            get { return _showDownloadComplete; }
            set { SetProperty(ref _showDownloadComplete, value); }
        }

        public ICommand BrowseInputCommand { get; private set; }
        public ICommand BrowseOutputCommand { get; private set; }
        public ICommand BrowseDownloadOutputCommand { get; private set; }
        public ICommand RemoveFileCommand { get; private set; }
        public ICommand ClearFilesCommand { get; private set; }
        public ICommand StartConversionCommand { get; private set; }
        public ICommand CancelConversionCommand { get; private set; }
        public ICommand StartDownloadCommand { get; private set; }
        public ICommand CancelDownloadCommand { get; private set; }
        public ICommand NavigateToConversionCommand { get; private set; }
        public ICommand NavigateToDownloadCommand { get; private set; }
        public ICommand OpenUpdateLogCommand { get; private set; }

        #region 歌单相关命令

        public ICommand CreatePlaylistCommand { get; private set; }
        public ICommand DeletePlaylistCommand { get; private set; }
        public ICommand RenamePlaylistCommand { get; private set; }
        public ICommand AddSongToPlaylistCommand { get; private set; }
        public ICommand RemoveSongFromPlaylistCommand { get; private set; }
        public ICommand AddConvertedFilesToPlaylistCommand { get; private set; }
        public ICommand BrowsePlaylistFolderCommand { get; private set; }
        public ICommand SelectPlaylistCommand { get; private set; }
        public ICommand OpenPlaylistDetailCommand { get; private set; }
        public ICommand SelectSongCommand { get; private set; }
        public ICommand CloseSelectedSongInfoCommand { get; private set; }
        public ICommand CopySelectedSongInfoCommand { get; private set; }
        public ICommand SetPlaylistCoverCommand { get; private set; }
        public ICommand RemovePlaylistCoverCommand { get; private set; }
        public ICommand BrowsePlaylistCoverCommand { get; private set; }
        public ICommand BrowsePlaylistsDirectoryCommand { get; private set; }
        public ICommand ShowAddToPlaylistDialogCommand { get; private set; }
        public ICommand HideAddToPlaylistDialogCommand { get; private set; }
        public ICommand AddConvertedFilesToExistingPlaylistCommand { get; private set; }
        public ICommand AddConvertedFilesToNewPlaylistCommand { get; private set; }
        public ICommand ImportToExistingPlaylistCommand { get; private set; }
        public ICommand ImportToNewPlaylistCommand { get; private set; }
        public ICommand SkipImportToPlaylistCommand { get; private set; }
        public ICommand ChangePlaylistDirectoryCommand { get; private set; }
        public ICommand GoBackToPlaylistsCommand { get; private set; }

        #endregion

        #region 音乐播放相关命令

        public ICommand PlaySongCommand { get; private set; }
        public ICommand PauseSongCommand { get; private set; }
        public ICommand SetPositionCommand { get; private set; }
        public ICommand OpenPlaylistCommand { get; private set; }
        public ICommand PreviousSongCommand { get; private set; }
        public ICommand NextSongCommand { get; private set; }

        #endregion

        public MainViewModel()
        {
            _converterService = new AudioConverterService();
            _downloadService = new NeteaseDownloadService();
            _musicPlayerService = new MusicPlayerService();
            _playlistStorageConfigPath = GetPlaylistStorageConfigPath();
            NcmFiles = new ObservableCollection<NcmFileInfo>();
            DownloadTasks = new ObservableCollection<DownloadTaskInfo>();
            Playlists = new ObservableCollection<PlaylistInfo>();
            ConvertedFiles = new ObservableCollection<string>();

            // 初始化歌单基础路径（可从配置文件读取）
            _playlistsBasePath = LoadPlaylistsBasePath();
            PlaylistsBaseDirectory = _playlistsBasePath;
            EnsureDirectoryExists(_playlistsBasePath);

            // 加载现有歌单
            LoadPlaylists();

            // 初始化音乐播放器事件
            InitializeMusicPlayer();

            BrowseInputCommand = new RelayCommand(_ => BrowseInput());
            BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());
            BrowseDownloadOutputCommand = new RelayCommand(_ => BrowseDownloadOutput());
            RemoveFileCommand = new RelayCommand(param => RemoveFile(param));
            ClearFilesCommand = new RelayCommand(_ => ClearFiles(), _ => NcmFiles.Count > 0);
            StartConversionCommand = new RelayCommand(_ => StartConversionAsync(), _ => CanStartConversion());
            CancelConversionCommand = new RelayCommand(_ => CancelConversion(), _ => IsConverting);
            StartDownloadCommand = new RelayCommand(_ => StartDownloadAsync(), _ => CanStartDownload());
            CancelDownloadCommand = new RelayCommand(_ => CancelDownload(), _ => IsDownloading);
            NavigateToConversionCommand = new RelayCommand(_ => SelectedTabIndex = 0);
            NavigateToDownloadCommand = new RelayCommand(_ => SelectedTabIndex = 1);
            OpenUpdateLogCommand = new RelayCommand(_ => OpenUpdateLog());

            // 歌单相关命令
            CreatePlaylistCommand = new RelayCommand(_ => CreatePlaylist(), _ => CanCreatePlaylist());
            DeletePlaylistCommand = new RelayCommand(_ => DeletePlaylist(), _ => CanDeletePlaylist());
            RenamePlaylistCommand = new RelayCommand(_ => RenamePlaylist(), _ => CanRenamePlaylist());
            AddSongToPlaylistCommand = new RelayCommand(param => AddSongToPlaylist(param), _ => CanAddSongToPlaylist());
            RemoveSongFromPlaylistCommand = new RelayCommand(param => RemoveSongFromPlaylist(param), _ => CanRemoveSongFromPlaylist());
            AddConvertedFilesToPlaylistCommand = new RelayCommand(_ => AddConvertedFilesToPlaylist(), _ => CanAddConvertedFilesToPlaylist());
            BrowsePlaylistFolderCommand = new RelayCommand(param => BrowsePlaylistFolder(param));
            SelectPlaylistCommand = new RelayCommand(param => SelectPlaylist(param));
            OpenPlaylistDetailCommand = new RelayCommand(param => OpenPlaylistDetail(param));
            SelectSongCommand = new RelayCommand(param => SelectSong(param));
            CloseSelectedSongInfoCommand = new RelayCommand(_ => CloseSelectedSongInfo(), _ => SelectedSong != null);
            CopySelectedSongInfoCommand = new RelayCommand(_ => CopySelectedSongInfo(), _ => SelectedSong != null);
            SetPlaylistCoverCommand = new RelayCommand(_ => SetPlaylistCover(), _ => CanSetPlaylistCover());
            RemovePlaylistCoverCommand = new RelayCommand(_ => RemovePlaylistCover(), _ => CanRemovePlaylistCover());
            BrowsePlaylistCoverCommand = new RelayCommand(_ => BrowsePlaylistCover());
            BrowsePlaylistsDirectoryCommand = new RelayCommand(_ => BrowsePlaylistsDirectory());
            ShowAddToPlaylistDialogCommand = new RelayCommand(_ => ShowAddToPlaylistDialog());
            HideAddToPlaylistDialogCommand = new RelayCommand(_ => HideAddToPlaylistDialog());
            AddConvertedFilesToExistingPlaylistCommand = new RelayCommand(param => AddConvertedFilesToExistingPlaylist(param), _ => CanAddConvertedFilesToExistingPlaylist());
            AddConvertedFilesToNewPlaylistCommand = new RelayCommand(_ => AddConvertedFilesToNewPlaylist(), _ => CanAddConvertedFilesToNewPlaylist());
            ImportToExistingPlaylistCommand = new RelayCommand(_ => ImportToExistingPlaylist(), _ => CanImportToExistingPlaylist());
            ImportToNewPlaylistCommand = new RelayCommand(_ => ImportToNewPlaylist(), _ => CanImportToNewPlaylist());
            SkipImportToPlaylistCommand = new RelayCommand(_ => SkipImportToPlaylist());
            ChangePlaylistDirectoryCommand = new RelayCommand(param => ChangePlaylistDirectory(param), _ => CanChangePlaylistDirectory());
            GoBackToPlaylistsCommand = new RelayCommand(_ => GoBackToPlaylists());

            // 音乐播放相关命令
            PlaySongCommand = new RelayCommand(param => PlaySong(param), _ => CanPlaySong());
            PauseSongCommand = new RelayCommand(_ => PauseSong(), _ => CanPauseSong());
            SetPositionCommand = new RelayCommand(param => SetPosition(param));
            OpenPlaylistCommand = new RelayCommand(param => OpenPlaylist(param), _ => CanOpenPlaylist());
            PreviousSongCommand = new RelayCommand(_ => PlayPreviousSong(), _ => CanPlayPreviousSong());
            NextSongCommand = new RelayCommand(_ => PlayNextSong(), _ => CanPlayNextSong());

            OutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Converted");
            DownloadOutputPath = OutputPath;

            EnsureDirectoryExists(OutputPath);
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch { }
            }
        }

        private void BrowseInput()
        {
            switch (InputMode)
            {
                case InputMode.SingleFile:
                    BrowseSingleFile();
                    break;
                case InputMode.BatchFolder:
                    BrowseFolder();
                    break;
                case InputMode.MultipleFiles:
                    BrowseMultipleFiles();
                    break;
            }
        }

        private void BrowseSingleFile()
        {
            Win32OpenFileDialog dialog = new Win32OpenFileDialog
            {
                Filter = "NCM 文件|*.ncm|所有文件|*.*",
                Title = "选择 NCM 文件"
            };

            if (dialog.ShowDialog() == true)
            {
                InputPath = dialog.FileName;
                NcmFiles.Clear();

                if (_converterService.IsValidNcmFile(dialog.FileName))
                {
                    FileInfo fileInfo = new FileInfo(dialog.FileName);
                    NcmFileInfo ncmFile = new NcmFileInfo();
                    ncmFile.FileName = fileInfo.Name;
                    ncmFile.FullPath = fileInfo.FullName;
                    ncmFile.FileSize = fileInfo.Length;
                    NcmFiles.Add(ncmFile);
                }
                else
                {
                    MessageBox.Show("选择的文件不是有效的 NCM 文件。", "无效文件", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void BrowseFolder()
        {
            using (FormsFolderBrowserDialog dialog = new FormsFolderBrowserDialog())
            {
                dialog.Description = "选择包含 NCM 文件的文件夹";
                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() == FormsDialogResult.OK)
                {
                    InputPath = dialog.SelectedPath;
                    LoadNcmFilesFromFolder(dialog.SelectedPath);
                }
            }
        }

        private void BrowseMultipleFiles()
        {
            Win32OpenFileDialog dialog = new Win32OpenFileDialog
            {
                Filter = "NCM 文件|*.ncm|所有文件|*.*",
                Title = "选择 NCM 文件",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                NcmFiles.Clear();
                List<string> validFiles = new List<string>();

                foreach (string fileName in dialog.FileNames)
                {
                    if (_converterService.IsValidNcmFile(fileName))
                    {
                        validFiles.Add(fileName);
                        FileInfo fileInfo = new FileInfo(fileName);
                        NcmFileInfo ncmFile = new NcmFileInfo();
                        ncmFile.FileName = fileInfo.Name;
                        ncmFile.FullPath = fileInfo.FullName;
                        ncmFile.FileSize = fileInfo.Length;
                        NcmFiles.Add(ncmFile);
                    }
                }

                if (validFiles.Count > 0)
                {
                    InputPath = string.Format("已选择 {0} 个文件", validFiles.Count);
                }
                else
                {
                    MessageBox.Show("未选择有效的 NCM 文件。", "无效文件", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void LoadNcmFilesFromFolder(string folderPath)
        {
            NcmFiles.Clear();
            List<NcmFileInfo> files = _converterService.ScanNcmFiles(folderPath, SearchOption.TopDirectoryOnly);

            foreach (NcmFileInfo file in files)
            {
                NcmFiles.Add(file);
            }

            ConversionStatusText = string.Format("找到 {0} 个 NCM 文件", NcmFiles.Count);
        }

        private void BrowseOutput()
        {
            using (FormsFolderBrowserDialog dialog = new FormsFolderBrowserDialog())
            {
                dialog.Description = "选择输出文件夹";
                dialog.ShowNewFolderButton = true;

                if (!string.IsNullOrEmpty(OutputPath) && Directory.Exists(OutputPath))
                {
                    dialog.SelectedPath = OutputPath;
                }

                if (dialog.ShowDialog() == FormsDialogResult.OK)
                {
                    OutputPath = dialog.SelectedPath;
                    if (UseSameOutputPath)
                    {
                        DownloadOutputPath = OutputPath;
                    }
                }
            }
        }

        private void BrowseDownloadOutput()
        {
            using (FormsFolderBrowserDialog dialog = new FormsFolderBrowserDialog())
            {
                dialog.Description = "选择下载保存文件夹";
                dialog.ShowNewFolderButton = true;

                if (!string.IsNullOrEmpty(DownloadOutputPath) && Directory.Exists(DownloadOutputPath))
                {
                    dialog.SelectedPath = DownloadOutputPath;
                }

                if (dialog.ShowDialog() == FormsDialogResult.OK)
                {
                    DownloadOutputPath = dialog.SelectedPath;
                }
            }
        }

        private void RemoveFile(object param)
        {
            if (param is NcmFileInfo fileInfo)
            {
                NcmFiles.Remove(fileInfo);
                ((RelayCommand)ClearFilesCommand).RaiseCanExecuteChanged();
            }
        }

        private void ClearFiles()
        {
            NcmFiles.Clear();
            InputPath = string.Empty;
            ((RelayCommand)ClearFilesCommand).RaiseCanExecuteChanged();
        }

        private bool CanStartConversion()
        {
            return !IsConverting && NcmFiles.Count > 0 && !string.IsNullOrEmpty(OutputPath);
        }

        private async void StartConversionAsync()
        {
            if (NcmFiles.Count == 0) return;

            EnsureDirectoryExists(OutputPath);
            _conversionCts = new CancellationTokenSource();

            IsConverting = true;
            ShowConversionComplete = false;
            ConversionProgress = 0;

            Progress<BatchConversionProgress> progress = new Progress<BatchConversionProgress>(p =>
            {
                CurrentConvertingFile = p.CurrentFile;
                ConversionProgress = p.OverallProgress;
                ConversionStatusText = string.Format("正在转换: {0} ({1}%)", p.CurrentFile, p.CurrentFileProgress);
            });

            try
            {
                List<NcmFileInfo> files = NcmFiles.ToList();
                BatchConversionResult result = await _converterService.ConvertBatchAsync(
                    files, OutputFormat, OutputPath, progress, _conversionCts.Token);

                ConversionStatusText = string.Format("转换完成: {0} 成功, {1} 失败, {2} 取消", 
                    result.CompletedFiles, result.FailedFiles, result.CancelledFiles);
                ShowConversionComplete = true;

                // 收集转换成功的文件路径
                ConvertedFiles.Clear();
                foreach (var file in result.ConvertedFilePaths)
                {
                    ConvertedFiles.Add(file);
                }

                // 显示导入歌单选项（如果有转换成功的文件）
                if (ConvertedFiles.Count > 0)
                {
                    ShowImportToPlaylistOptions = true;
                }

                await Task.Delay(3000);
                ShowConversionComplete = false;
            }
            catch (OperationCanceledException)
            {
                ConversionStatusText = "转换已取消";
            }
            catch (Exception ex)
            {
                ConversionStatusText = string.Format("转换出错: {0}", ex.Message);
                MessageBox.Show(string.Format("转换过程中发生错误:\n{0}", ex.Message), "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsConverting = false;
                if (_conversionCts != null)
                {
                    _conversionCts.Dispose();
                    _conversionCts = null;
                }
            }
        }

        private void CancelConversion()
        {
            if (_conversionCts != null)
            {
                _conversionCts.Cancel();
            }
            ConversionStatusText = "正在取消...";
        }

        private bool CanStartDownload()
        {
            return !IsDownloading && _downloadService.IsValidSongId(SongId) && !string.IsNullOrEmpty(DownloadOutputPath);
        }

        private async void StartDownloadAsync()
        {
            if (!_downloadService.IsValidSongId(SongId)) return;

            EnsureDirectoryExists(DownloadOutputPath);
            _downloadCts = new CancellationTokenSource();

            IsDownloading = true;
            ShowDownloadComplete = false;
            DownloadProgress = 0;

            DownloadTaskInfo taskInfo = new DownloadTaskInfo { SongId = SongId };
            DownloadTasks.Add(taskInfo);

            Progress<DownloadProgress> progress = new Progress<DownloadProgress>(p =>
            {
                DownloadProgress = p.Progress;
                DownloadStatusText = string.Format("{0} ({1}%)", p.Phase, p.Progress);
            });

            try
            {
                DownloadResult result = await _downloadService.DownloadAsync(
                    taskInfo, DownloadOutputPath, progress, _downloadCts.Token);

                if (result.Success)
                {
                    DownloadStatusText = string.Format("下载完成: {0}", result.FileName);
                    ShowDownloadComplete = true;

                    await Task.Delay(3000);
                    ShowDownloadComplete = false;
                }
                else if (result.Cancelled)
                {
                    DownloadStatusText = "下载已取消";
                }
                else
                {
                    DownloadStatusText = string.Format("下载失败: {0}", result.ErrorMessage);
                    MessageBox.Show(string.Format("下载失败:\n{0}", result.ErrorMessage), "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                DownloadStatusText = string.Format("下载出错: {0}", ex.Message);
                MessageBox.Show(string.Format("下载过程中发生错误:\n{0}", ex.Message), "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsDownloading = false;
                if (_downloadCts != null)
                {
                    _downloadCts.Dispose();
                    _downloadCts = null;
                }
            }
        }

        private void CancelDownload()
        {
            if (_downloadCts != null)
            {
                _downloadCts.Cancel();
            }
            DownloadStatusText = "正在取消...";
        }

        private void OpenUpdateLog()
        {
            try
            {
                var updateLogWindow = new 网易云音乐下载.UpdateLogWindow();
                if (Application.Current != null && Application.Current.MainWindow != null)
                {
                    updateLogWindow.Owner = Application.Current.MainWindow;
                }

                updateLogWindow.Show();
            }
            catch (Exception ex)
            {
                PlaylistStatusText = string.Format("打开更新日志失败: {0}", ex.Message);
            }
        }

        #region 歌单相关方法

        private void LoadPlaylists()
        {
            try
            {
                Playlists.Clear();

                foreach (var dir in GetKnownPlaylistDirectories())
                {
                    var playlist = new PlaylistInfo
                    {
                        Name = Path.GetFileName(dir),
                        FolderPath = dir,
                        CreatedTime = Directory.GetCreationTime(dir)
                    };

                    // 加载歌单中的歌曲
                    LoadSongsForPlaylist(playlist);
                    playlist.CheckAndLoadCover();
                    Playlists.Add(playlist);
                }

                SavePlaylistStorageState();
            }
            catch (Exception ex)
            {
                PlaylistStatusText = string.Format("加载歌单失败: {0}", ex.Message);
            }
        }

        private void LoadSongsForPlaylist(PlaylistInfo playlist)
        {
            try
            {
                if (!Directory.Exists(playlist.FolderPath))
                    return;

                var audioFiles = Directory.GetFiles(playlist.FolderPath, "*.mp3")
                    .Concat(Directory.GetFiles(playlist.FolderPath, "*.flac"))
                    .Concat(Directory.GetFiles(playlist.FolderPath, "*.wav"))
                    .Concat(Directory.GetFiles(playlist.FolderPath, "*.ncm"));

                foreach (var file in audioFiles)
                {
                    var song = PlaylistSongInfo.FromFilePath(file);
                    if (song != null)
                    {
                        playlist.AddSong(song);
                    }
                }
            }
            catch { }
        }

        private bool CanCreatePlaylist()
        {
            return !string.IsNullOrWhiteSpace(NewPlaylistName);
        }

        private void CreatePlaylist()
        {
            try
            {
                string inputText = NewPlaylistName.Trim();
                string folderPath = ResolvePlaylistFolderPath(inputText);
                string playlistName = Path.GetFileName(folderPath);

                if (Directory.Exists(folderPath))
                {
                    MessageBox.Show(string.Format("歌单文件夹 \"{0}\" 已存在。", folderPath), "创建失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Directory.CreateDirectory(folderPath);
                PlaylistInfo.EnsureMarkerFile(folderPath);

                var playlist = new PlaylistInfo
                {
                    Name = playlistName,
                    FolderPath = folderPath,
                    CreatedTime = DateTime.Now
                };

                if (!string.IsNullOrEmpty(NewPlaylistCoverPath) && File.Exists(NewPlaylistCoverPath))
                {
                    playlist.SetCoverImage(NewPlaylistCoverPath);
                }

                Playlists.Add(playlist);
                SelectedPlaylist = playlist;
                NewPlaylistName = string.Empty;
                NewPlaylistCoverPath = string.Empty;
                SavePlaylistStorageState();
                PlaylistStatusText = string.Format("歌单 \"{0}\" 创建成功", playlistName);
            }
            catch (Exception ex)
            {
                PlaylistStatusText = string.Format("创建歌单失败: {0}", ex.Message);
            }
        }

        private bool CanDeletePlaylist()
        {
            return SelectedPlaylist != null;
        }

        private void DeletePlaylist()
        {
            if (SelectedPlaylist == null) return;

            var result = MessageBox.Show(
                string.Format("确定要删除歌单 \"{0}\" 吗？\n歌单中的歌曲文件将被移动到回收站。", SelectedPlaylist.Name),
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                // 删除文件夹
                if (Directory.Exists(SelectedPlaylist.FolderPath))
                {
                    Directory.Delete(SelectedPlaylist.FolderPath, true);
                }

                PlaylistStatusText = string.Format("歌单 \"{0}\" 已删除", SelectedPlaylist.Name);
                Playlists.Remove(SelectedPlaylist);
                SelectedPlaylist = null;
                SavePlaylistStorageState();
            }
            catch (Exception ex)
            {
                PlaylistStatusText = string.Format("删除歌单失败: {0}", ex.Message);
            }
        }

        private bool CanRenamePlaylist()
        {
            return SelectedPlaylist != null && !string.IsNullOrWhiteSpace(NewPlaylistName);
        }

        private void RenamePlaylist()
        {
            if (SelectedPlaylist == null) return;

            try
            {
                string newName = NewPlaylistName.Trim();
                if (SelectedPlaylist.Rename(newName))
                {
                    NewPlaylistName = string.Empty;
                    SavePlaylistStorageState();
                    PlaylistStatusText = string.Format("歌单已重命名为 \"{0}\"", newName);
                }
                else
                {
                    MessageBox.Show(string.Format("无法重命名歌单，名称可能已存在。"), "重命名失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                PlaylistStatusText = string.Format("重命名歌单失败: {0}", ex.Message);
            }
        }

        private PlaylistInfo CurrentPlaylist
        {
            get { return IsPlaylistDetailView ? PlaylistDetailPlaylist : SelectedPlaylist; }
        }

        private bool CanAddSongToPlaylist()
        {
            return CurrentPlaylist != null;
        }

        private void AddSongToPlaylist(object param)
        {
            var playlist = param as PlaylistInfo ?? CurrentPlaylist;
            if (playlist == null) return;

            var dialog = new Win32OpenFileDialog
            {
                Filter = "音频文件|*.mp3;*.flac;*.wav;*.ncm|所有文件|*.*",
                Title = "选择要添加到歌单的歌曲",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                int addedCount = 0;
                foreach (var filePath in dialog.FileNames)
                {
                    try
                    {
                        // 复制文件到歌单文件夹
                        string destPath = Path.Combine(playlist.FolderPath, Path.GetFileName(filePath));
                        if (!File.Exists(destPath))
                        {
                            File.Copy(filePath, destPath);
                        }

                        // 添加到歌单
                        var song = PlaylistSongInfo.FromFilePath(destPath);
                        if (song != null)
                        {
                            playlist.AddSong(song);
                            addedCount++;
                        }
                    }
                    catch { }
                }

                PlaylistStatusText = string.Format("已添加 {0} 首歌曲到歌单 \"{1}\"", addedCount, playlist.Name);
            }
        }

        private bool CanRemoveSongFromPlaylist()
        {
            return CurrentPlaylist != null && CurrentPlaylist.Songs.Count > 0;
        }

        private void RemoveSongFromPlaylist(object param)
        {
            var playlist = CurrentPlaylist;
            if (playlist == null || param == null) return;

            var song = param as PlaylistSongInfo;
            if (song == null) return;

            var result = MessageBox.Show(
                string.Format("确定要从歌单中删除 \"{0}\" 吗？\n文件将被永久删除。", song.DisplayName),
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                // 从歌单移除
                playlist.RemoveSong(song);

                // 删除文件
                if (File.Exists(song.FilePath))
                {
                    File.Delete(song.FilePath);
                }

                // 如果删除的是当前播放的歌曲，停止播放
                if (CurrentSong != null && CurrentSong.FilePath == song.FilePath)
                {
                    _musicPlayerService.Stop();
                }

                PlaylistStatusText = string.Format("已从歌单移除 \"{0}\"", song.DisplayName);
            }
            catch (Exception ex)
            {
                PlaylistStatusText = string.Format("移除歌曲失败: {0}", ex.Message);
            }
        }

        private bool CanAddConvertedFilesToPlaylist()
        {
            return SelectedPlaylist != null;
        }

        private void AddConvertedFilesToPlaylist()
        {
            if (SelectedPlaylist == null) return;

            var dialog = new Win32OpenFileDialog
            {
                Filter = "音频文件|*.mp3;*.flac;*.wav|所有文件|*.*",
                Title = "选择转换后的歌曲添加到歌单",
                Multiselect = true,
                InitialDirectory = OutputPath
            };

            if (dialog.ShowDialog() == true)
            {
                int addedCount = 0;
                foreach (var filePath in dialog.FileNames)
                {
                    try
                    {
                        // 复制文件到歌单文件夹
                        string destPath = Path.Combine(SelectedPlaylist.FolderPath, Path.GetFileName(filePath));
                        if (!File.Exists(destPath))
                        {
                            File.Copy(filePath, destPath);
                        }

                        // 添加到歌单
                        var song = PlaylistSongInfo.FromFilePath(destPath);
                        if (song != null)
                        {
                            SelectedPlaylist.AddSong(song);
                            addedCount++;
                        }
                    }
                    catch { }
                }

                PlaylistStatusText = string.Format("已添加 {0} 首歌曲到歌单 \"{1}\"", addedCount, SelectedPlaylist.Name);
            }
        }

        private void BrowsePlaylistFolder(object param)
        {
            var playlist = param as PlaylistInfo ?? CurrentPlaylist ?? SelectedPlaylist;
            if (playlist == null) return;

            try
            {
                if (Directory.Exists(playlist.FolderPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", playlist.FolderPath);
                }
            }
            catch (Exception ex)
            {
                PlaylistStatusText = string.Format("打开文件夹失败: {0}", ex.Message);
            }
        }

        private void SelectPlaylist(object param)
        {
            if (param is PlaylistInfo playlist)
            {
                SelectedPlaylist = playlist;
            }
        }

        private bool CanSetPlaylistCover()
        {
            return SelectedPlaylist != null;
        }

        private void SetPlaylistCover()
        {
            if (SelectedPlaylist == null) return;

            var dialog = new Win32OpenFileDialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp|所有文件|*.*",
                Title = "选择歌单封面图片"
            };

            if (dialog.ShowDialog() == true)
            {
                if (SelectedPlaylist.SetCoverImage(dialog.FileName))
                {
                    PlaylistStatusText = string.Format("歌单 \"{0}\" 封面已更新", SelectedPlaylist.Name);
                }
                else
                {
                    PlaylistStatusText = "设置封面失败";
                }
            }
        }

        private bool CanRemovePlaylistCover()
        {
            return SelectedPlaylist != null && SelectedPlaylist.HasCustomCover;
        }

        private void RemovePlaylistCover()
        {
            if (SelectedPlaylist == null) return;

            if (SelectedPlaylist.RemoveCoverImage())
            {
                PlaylistStatusText = string.Format("歌单 \"{0}\" 封面已恢复默认", SelectedPlaylist.Name);
            }
        }

        private void BrowsePlaylistCover()
        {
            var dialog = new Win32OpenFileDialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp|所有文件|*.*",
                Title = "选择歌单封面图片"
            };

            if (dialog.ShowDialog() == true)
            {
                NewPlaylistCoverPath = dialog.FileName;
            }
        }

        private void BrowsePlaylistsDirectory()
        {
            using (var dialog = new FormsFolderBrowserDialog())
            {
                dialog.Description = "选择歌单存储目录";
                dialog.SelectedPath = _playlistsBasePath;

                if (dialog.ShowDialog() == FormsDialogResult.OK)
                {
                    string newPath = dialog.SelectedPath;
                    if (newPath != _playlistsBasePath)
                    {
                        // 移动现有歌单到新目录
                        try
                        {
                            MovePlaylistsToNewDirectory(newPath);
                            _playlistsBasePath = newPath;
                            PlaylistsBaseDirectory = newPath;
                            SavePlaylistsBasePath(newPath);
                            SavePlaylistStorageState();
                            PlaylistStatusText = "歌单存储目录已更改";
                        }
                        catch (Exception ex)
                        {
                            PlaylistStatusText = string.Format("更改目录失败: {0}", ex.Message);
                        }
                    }
                }
            }
        }

        private void ShowAddToPlaylistDialog()
        {
            IsAddToPlaylistDialogVisible = true;
        }

        private void HideAddToPlaylistDialog()
        {
            IsAddToPlaylistDialogVisible = false;
            NewPlaylistName = string.Empty;
            NewPlaylistCoverPath = string.Empty;
        }

        private bool CanAddConvertedFilesToExistingPlaylist()
        {
            return SelectedPlaylist != null && ConvertedFiles.Count > 0;
        }

        private void AddConvertedFilesToExistingPlaylist(object param)
        {
            if (SelectedPlaylist == null || ConvertedFiles.Count == 0) return;

            int addedCount = 0;
            foreach (var filePath in ConvertedFiles)
            {
                try
                {
                    string destPath = Path.Combine(SelectedPlaylist.FolderPath, Path.GetFileName(filePath));
                    if (!File.Exists(destPath))
                    {
                        File.Copy(filePath, destPath);
                    }

                    var song = PlaylistSongInfo.FromFilePath(destPath);
                    if (song != null)
                    {
                        SelectedPlaylist.AddSong(song);
                        addedCount++;
                    }
                }
                catch { }
            }

            PlaylistStatusText = string.Format("已添加 {0} 首歌曲到歌单 \"{1}\"", addedCount, SelectedPlaylist.Name);
            HideAddToPlaylistDialog();
        }

        private bool CanAddConvertedFilesToNewPlaylist()
        {
            return !string.IsNullOrWhiteSpace(NewPlaylistName) && ConvertedFiles.Count > 0;
        }

        private void AddConvertedFilesToNewPlaylist()
        {
            if (string.IsNullOrWhiteSpace(NewPlaylistName) || ConvertedFiles.Count == 0) return;

            try
            {
                string inputText = NewPlaylistName.Trim();
                string folderPath = ResolvePlaylistFolderPath(inputText);
                string playlistName = Path.GetFileName(folderPath);

                if (Directory.Exists(folderPath))
                {
                    MessageBox.Show(string.Format("歌单文件夹 \"{0}\" 已存在。", folderPath), "创建失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Directory.CreateDirectory(folderPath);
                PlaylistInfo.EnsureMarkerFile(folderPath);

                var playlist = new PlaylistInfo
                {
                    Name = playlistName,
                    FolderPath = folderPath,
                    CreatedTime = DateTime.Now
                };

                // 设置封面
                if (!string.IsNullOrEmpty(NewPlaylistCoverPath) && File.Exists(NewPlaylistCoverPath))
                {
                    playlist.SetCoverImage(NewPlaylistCoverPath);
                }

                // 添加歌曲
                int addedCount = 0;
                foreach (var filePath in ConvertedFiles)
                {
                    try
                    {
                        string destPath = Path.Combine(folderPath, Path.GetFileName(filePath));
                        if (!File.Exists(destPath))
                        {
                            File.Copy(filePath, destPath);
                        }

                        var song = PlaylistSongInfo.FromFilePath(destPath);
                        if (song != null)
                        {
                            playlist.AddSong(song);
                            addedCount++;
                        }
                    }
                    catch { }
                }

                Playlists.Add(playlist);
                SelectedPlaylist = playlist;
                SavePlaylistStorageState();
                PlaylistStatusText = string.Format("已创建歌单 \"{0}\" 并添加 {1} 首歌曲", playlistName, addedCount);
                HideAddToPlaylistDialog();
            }
            catch (Exception ex)
            {
                PlaylistStatusText = string.Format("创建歌单失败: {0}", ex.Message);
            }
        }

        private string LoadPlaylistsBasePath()
        {
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Playlists");

            try
            {
                var state = LoadPlaylistStorageState();
                if (!string.IsNullOrWhiteSpace(state.BaseDirectory))
                {
                    return state.BaseDirectory;
                }
            }
            catch { }

            return defaultPath;
        }

        private void SavePlaylistsBasePath(string path)
        {
            _playlistsBasePath = path;
        }

        private void MovePlaylistsToNewDirectory(string newPath)
        {
            if (!Directory.Exists(newPath))
            {
                Directory.CreateDirectory(newPath);
            }

            // 移动所有歌单文件夹
            foreach (var playlist in Playlists)
            {
                string newFolderPath = Path.Combine(newPath, playlist.Name);
                if (Directory.Exists(playlist.FolderPath) && !Directory.Exists(newFolderPath))
                {
                    Directory.Move(playlist.FolderPath, newFolderPath);
                    playlist.FolderPath = newFolderPath;

                    // 更新封面路径
                    if (playlist.HasCustomCover)
                    {
                        playlist.CoverImagePath = Path.Combine(newFolderPath, PlaylistInfo.CoverFileName);
                    }

                    // 更新歌曲路径
                    foreach (var song in playlist.Songs)
                    {
                        song.UpdateFolderPath(newFolderPath);
                    }
                }
            }

            SavePlaylistStorageState();
        }

        // 导入到现有歌单
        private bool CanImportToExistingPlaylist()
        {
            return Playlists.Count > 0 && ConvertedFiles.Count > 0;
        }

        private void ImportToExistingPlaylist()
        {
            if (Playlists.Count == 0 || ConvertedFiles.Count == 0) return;

            // 如果只有一个歌单，直接导入
            if (Playlists.Count == 1)
            {
                SelectedPlaylist = Playlists[0];
                AddConvertedFilesToExistingPlaylist(null);
                ShowImportToPlaylistOptions = false;
                return;
            }

            // 显示选择歌单对话框
            var playlistNames = Playlists.Select(p => p.Name).ToArray();
            string selectedName = ShowPlaylistSelectionDialog(playlistNames);

            if (!string.IsNullOrEmpty(selectedName))
            {
                var playlist = Playlists.FirstOrDefault(p => p.Name == selectedName);
                if (playlist != null)
                {
                    SelectedPlaylist = playlist;
                    AddConvertedFilesToExistingPlaylist(null);
                }
            }
            ShowImportToPlaylistOptions = false;
        }

        // 导入到新歌单
        private bool CanImportToNewPlaylist()
        {
            return ConvertedFiles.Count > 0;
        }

        private void ImportToNewPlaylist()
        {
            if (ConvertedFiles.Count == 0) return;

            // 显示输入歌单名称对话框
            string playlistName = ShowInputDialog("创建新歌单", "请输入歌单名称：");

            if (!string.IsNullOrWhiteSpace(playlistName))
            {
                NewPlaylistName = playlistName;
                AddConvertedFilesToNewPlaylist();
            }
            ShowImportToPlaylistOptions = false;
        }

        // 跳过导入
        private void SkipImportToPlaylist()
        {
            ShowImportToPlaylistOptions = false;
            ConvertedFiles.Clear();
        }

        // 显示歌单选择对话框
        private string ShowPlaylistSelectionDialog(string[] playlistNames)
        {
            // 构建消息内容
            string message = "请选择要导入的歌单：\n\n";
            for (int i = 0; i < playlistNames.Length; i++)
            {
                message += string.Format("{i + 1}. {playlistNames[i]}\n");
            }
            message += "\n请输入歌单名称或序号：";

            // 使用自定义的输入对话框
            var inputDialog = new InputDialog("选择歌单", message, playlistNames[0]);
            if (inputDialog.ShowDialog() == true)
            {
                string input = inputDialog.InputText;
                if (!string.IsNullOrEmpty(input))
                {
                    // 如果输入的是数字，转换为歌单名称
                    if (int.TryParse(input, out int index) && index > 0 && index <= playlistNames.Length)
                    {
                        return playlistNames[index - 1];
                    }
                    // 否则直接返回输入的名称
                    return input;
                }
            }
            return null;
        }

        // 显示输入对话框
        private string ShowInputDialog(string title, string prompt)
        {
            var inputDialog = new InputDialog(title, prompt, "");
            if (inputDialog.ShowDialog() == true)
            {
                return inputDialog.InputText;
            }
            return null;
        }

        #endregion

        #region 音乐播放相关方法

        private void InitializeMusicPlayer()
        {
            _musicPlayerService.PlaybackStarted += (s, e) =>
            {
                IsPlaying = true;
            };

            _musicPlayerService.PlaybackPaused += (s, e) =>
            {
                IsPlaying = false;
            };

            _musicPlayerService.PlaybackStopped += (s, e) =>
            {
                IsPlaying = false;
                UpdatePlaybackPosition(TimeSpan.Zero);
            };

            _musicPlayerService.PlaybackCompleted += (s, e) =>
            {
                IsPlaying = false;
                UpdatePlaybackPosition(TimeSpan.Zero);
                // 自动播放下一首
                PlayNextSong();
            };

            _musicPlayerService.PositionChanged += (s, position) =>
            {
                if (_isUserSeeking)
                {
                    return;
                }

                UpdatePlaybackPosition(position);
            };

            _musicPlayerService.DurationChanged += (s, duration) =>
            {
                TotalDuration = duration;
                TotalDurationSeconds = duration.TotalSeconds;
            };
        }

        private void SelectSong(object param)
        {
            if (param is PlaylistSongInfo song)
            {
                SelectedSong = song;
            }
        }

        private void CloseSelectedSongInfo()
        {
            SelectedSong = null;
        }

        private void CopySelectedSongInfo()
        {
            if (SelectedSong == null)
            {
                return;
            }

            string text = string.Format(
                "歌曲标题: {0}\r\n歌手名称: {1}\r\n专辑名称: {2}\r\n播放时长: {3}\r\n文件大小: {4}\r\n文件名称: {5}\r\n文件路径: {6}",
                SelectedSong.TitleDisplay,
                SelectedSong.ArtistDisplay,
                SelectedSong.AlbumDisplay,
                SelectedSong.DurationText,
                SelectedSong.FileSizeText,
                SelectedSong.FileName,
                SelectedSong.FilePath);

            Clipboard.SetText(text);
            PlaylistStatusText = "音频信息已复制到剪贴板";
        }

        private bool CanPlaySong()
        {
            return SelectedPlaylist != null || PlaylistDetailPlaylist != null;
        }

        private void OpenPlaylistDetail(object param)
        {
            if (param is PlaylistInfo playlist)
            {
                // Load songs for the playlist
                LoadSongsForPlaylist(playlist);
                
                // Switch to playlist detail view
                SelectedTabIndex = 2;
                IsPlaylistDetailView = true;
                PlaylistDetailPlaylist = playlist;
            }
        }

        private void PlaySong(object param)
        {
            if (param is PlaylistSongInfo song)
            {
                CurrentSong = song;
                CurrentPlayingSongPath = song.FilePath;
                CurrentPlayingSongName = song.DisplayName;
                _musicPlayerService.Play(song.FilePath);
                IsPlayerVisible = true;
            }
        }

        private bool CanPauseSong()
        {
            return _musicPlayerService.IsPlaying || _musicPlayerService.IsPaused;
        }

        private void PauseSong()
        {
            if (_musicPlayerService.IsPaused)
            {
                _musicPlayerService.Resume();
            }
            else
            {
                _musicPlayerService.Pause();
            }
        }

        private void SetPosition(object param)
        {
            if (param is TimeSpan position)
            {
                _musicPlayerService.SetPosition(position);
            }
        }

        private bool CanOpenPlaylist()
        {
            return SelectedPlaylist != null;
        }

        private void OpenPlaylist(object param)
        {
            if (param is PlaylistInfo playlist)
            {
                PlaylistDetailPlaylist = playlist;
                IsPlaylistDetailView = true;
                // Reload songs to ensure latest data
                LoadSongsForPlaylist(playlist);
                PlaylistStatusText = string.Format("已打开歌单 \"{0}\"", playlist.Name);
            }
        }

        private bool CanChangePlaylistDirectory()
        {
            return PlaylistDetailPlaylist != null;
        }

        private void ChangePlaylistDirectory(object param)
        {
            PlaylistInfo playlist = null;
            if (param is PlaylistInfo p)
            {
                playlist = p;
            }
            else
            {
                playlist = PlaylistDetailPlaylist ?? SelectedPlaylist;
            }

            if (playlist == null) return;

            using (var dialog = new FormsFolderBrowserDialog())
            {
                dialog.Description = string.Format("选择歌单 \"{0}\" 的新存储位置", playlist.Name);
                dialog.SelectedPath = Path.GetDirectoryName(playlist.FolderPath);

                if (dialog.ShowDialog() == FormsDialogResult.OK)
                {
                    string newDirectoryPath = dialog.SelectedPath;
                    try
                    {
                        // Get the new full path with the same folder name
                        string folderName = Path.GetFileName(playlist.FolderPath);
                        string newPath = Path.Combine(newDirectoryPath, folderName);

                        if (string.Equals(
                            Path.GetFullPath(newPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                            Path.GetFullPath(playlist.FolderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                            StringComparison.OrdinalIgnoreCase))
                        {
                            PlaylistStatusText = "歌单目录未变更";
                            return;
                        }

                        // If the target folder already exists but is different, show error
                        if (Directory.Exists(newPath) && newPath != playlist.FolderPath)
                        {
                            MessageBox.Show(string.Format("目标位置已存在同名歌单文件夹。\n请选择其他位置。"), "更改目录失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Move the folder (support cross-volume move)
                        MoveDirectory(playlist.FolderPath, newPath);

                        // Update the playlist path
                        playlist.FolderPath = newPath;

                        // Update all song paths
                        foreach (var song in playlist.Songs)
                        {
                            song.UpdateFolderPath(newPath);
                        }

                        if (playlist.HasCustomCover)
                        {
                            playlist.CoverImagePath = Path.Combine(newPath, PlaylistInfo.CoverFileName);
                        }

                        // Also update the selected playlist if it's the same
                        if (SelectedPlaylist == playlist)
                        {
                            SelectedPlaylist = playlist; // trigger refresh
                        }

                        SavePlaylistStorageState();
                        PlaylistStatusText = string.Format("歌单 \"{0}\" 目录已更改", playlist.Name);
                    }
                    catch (Exception ex)
                    {
                        PlaylistStatusText = string.Format("更改目录失败: {0}", ex.Message);
                        MessageBox.Show(string.Format("更改目录失败:\n{0}", ex.Message), "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        /// <summary>
        /// 移动目录（支持跨卷移动）
        /// </summary>
        private void MoveDirectory(string sourcePath, string destinationPath)
        {
            string sourceRoot = Path.GetPathRoot(sourcePath);
            string destinationRoot = Path.GetPathRoot(destinationPath);

            if (!string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                CopyDirectory(sourcePath, destinationPath, true);
                Directory.Delete(sourcePath, true);
                return;
            }

            try
            {
                // Try normal move first (faster, same volume)
                Directory.Move(sourcePath, destinationPath);
            }
            catch
            {
                CopyDirectory(sourcePath, destinationPath, true);
                Directory.Delete(sourcePath, true);
            }
        }

        /// <summary>
        /// 复制目录
        /// </summary>
        private void CopyDirectory(string sourcePath, string destinationPath, bool overwrite)
        {
            // Create destination directory
            Directory.CreateDirectory(destinationPath);

            // Copy all files
            foreach (var file in Directory.GetFiles(sourcePath))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destinationPath, fileName);
                File.Copy(file, destFile, overwrite);
            }

            // Copy all subdirectories
            foreach (var dir in Directory.GetDirectories(sourcePath))
            {
                string dirName = Path.GetFileName(dir);
                string destDir = Path.Combine(destinationPath, dirName);
                CopyDirectory(dir, destDir, overwrite);
            }
        }

        private void GoBackToPlaylists()
        {
            IsPlaylistDetailView = false;
            PlaylistDetailPlaylist = null;
        }

        public void BeginSeek()
        {
            if (!CanSeekCurrentSong)
            {
                return;
            }

            _isUserSeeking = true;
        }

        public void CompleteSeek(double sliderValue)
        {
            if (!_isUserSeeking)
            {
                return;
            }

            _isUserSeeking = false;
            SeekTo(sliderValue);
        }

        private void SeekTo(double seconds)
        {
            double clampedSeconds = ClampSeekSeconds(seconds);
            var targetPosition = TimeSpan.FromSeconds(clampedSeconds);

            UpdatePlaybackPosition(targetPosition);

            if (_musicPlayerService != null)
            {
                _musicPlayerService.SetPosition(targetPosition);
            }
        }

        private void UpdatePlaybackPosition(TimeSpan position)
        {
            _isSyncingPlaybackPosition = true;
            try
            {
                CurrentPosition = position;
                CurrentPositionSeconds = position.TotalSeconds;
            }
            finally
            {
                _isSyncingPlaybackPosition = false;
            }
        }

        private double ClampSeekSeconds(double seconds)
        {
            double maxDuration = TotalDurationSeconds > 0 ? TotalDurationSeconds : Math.Max(0, seconds);
            return Math.Max(0, Math.Min(seconds, maxDuration));
        }

        private string ResolvePlaylistFolderPath(string playlistInput)
        {
            if (string.IsNullOrWhiteSpace(playlistInput))
            {
                throw new InvalidOperationException("请输入歌单名称或完整文件夹路径。");
            }

            string folderPath = Path.IsPathRooted(playlistInput)
                ? playlistInput
                : Path.Combine(_playlistsBasePath, playlistInput);

            folderPath = Path.GetFullPath(folderPath.Trim());
            string playlistName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                throw new InvalidOperationException("歌单路径无效，请重新输入。");
            }

            return folderPath;
        }

        private IEnumerable<string> GetKnownPlaylistDirectories()
        {
            var playlistDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // 1. 处理已保存的歌单路径
                foreach (var storedPath in LoadPlaylistStorageState().PlaylistPaths)
                {
                    if (!string.IsNullOrWhiteSpace(storedPath) && Directory.Exists(storedPath))
                    {
                        string fullPath = Path.GetFullPath(storedPath);
                        if (IsRecognizedPlaylistFolder(fullPath))
                        {
                            // 如果是旧标记，自动升级为新标记
                            if (PlaylistInfo.HasLegacyMarkerFile(fullPath))
                            {
                                PlaylistInfo.EnsureMarkerFile(fullPath);
                            }
                            playlistDirectories.Add(fullPath);
                        }
                        else if (CanMigrateLegacyPlaylist(fullPath))
                        {
                            // 迁移逻辑：对于没有标记但看起来像歌单的目录，补上标记
                            PlaylistInfo.EnsureMarkerFile(fullPath);
                            playlistDirectories.Add(fullPath);
                        }
                    }
                }
            }
            catch { }

            // 2. 扫描基础歌单目录
            if (Directory.Exists(_playlistsBasePath))
            {
                foreach (var dir in Directory.GetDirectories(_playlistsBasePath))
                {
                    string fullPath = Path.GetFullPath(dir);
                    if (IsRecognizedPlaylistFolder(fullPath))
                    {
                        // 基础目录下的也检查是否需要升级标记
                        if (PlaylistInfo.HasLegacyMarkerFile(fullPath))
                        {
                            PlaylistInfo.EnsureMarkerFile(fullPath);
                        }
                        playlistDirectories.Add(fullPath);
                    }
                }
            }

            return playlistDirectories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        }

        private PlaylistStorageState LoadPlaylistStorageState()
        {
            try
            {
                if (File.Exists(_playlistStorageConfigPath))
                {
                    string json = File.ReadAllText(_playlistStorageConfigPath);
                    var state = JsonSerializer.Deserialize<PlaylistStorageState>(json);
                    if (state != null)
                    {
                        state.PlaylistPaths ??= new List<string>();
                        return state;
                    }
                }
            }
            catch { }

            return new PlaylistStorageState();
        }

        private void SavePlaylistStorageState()
        {
            try
            {
                string configDirectory = Path.GetDirectoryName(_playlistStorageConfigPath);
                if (!string.IsNullOrEmpty(configDirectory))
                {
                    Directory.CreateDirectory(configDirectory);
                }

                var state = new PlaylistStorageState
                {
                    BaseDirectory = _playlistsBasePath,
                    PlaylistPaths = Playlists
                        .Select(p => p.FolderPath)
                        .Where(path => !string.IsNullOrWhiteSpace(path))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(_playlistStorageConfigPath, JsonSerializer.Serialize(state, options));
            }
            catch { }
        }

        private string GetPlaylistStorageConfigPath()
        {
            string configDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "网易云音乐下载");
            return Path.Combine(configDirectory, "playlists.json");
        }

        private bool IsRecognizedPlaylistFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return false;
            }

            // 1. 如果有有效的 V2 标记，直接认领（即使是空目录，也可能是刚创建的）
            if (PlaylistInfo.HasValidMarkerFile(folderPath))
            {
                return true;
            }

            // 2. 如果是旧标记，必须目录结构像歌单（且包含音频文件）才认领
            if (PlaylistInfo.HasLegacyMarkerFile(folderPath))
            {
                return HasValidPlaylistFolderStructure(folderPath, requireAudioFiles: true);
            }

            return false;
        }

        private bool CanMigrateLegacyPlaylist(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return false;
            }

            // 如果已经有标记了，就不走迁移逻辑（由 IsRecognizedPlaylistFolder 处理）
            if (PlaylistInfo.HasMarkerFile(folderPath))
            {
                return false;
            }

            // 如果在默认歌单目录下，通常由扫描逻辑处理
            if (IsPathUnderDirectory(folderPath, _playlistsBasePath))
            {
                return false;
            }

            // 只有当目录结构非常像歌单（且有音频文件）时才允许迁移
            return HasValidPlaylistFolderStructure(folderPath, requireAudioFiles: true);
        }

        private bool HasValidPlaylistFolderStructure(string folderPath, bool requireAudioFiles = false)
        {
            try
            {
                // 不允许有子目录
                if (Directory.GetDirectories(folderPath).Length > 0)
                {
                    return false;
                }

                var files = Directory.GetFiles(folderPath);
                bool hasAudio = false;

                foreach (var filePath in files)
                {
                    string fileName = Path.GetFileName(filePath);
                    // 忽略标记文件和封面文件
                    if (string.Equals(fileName, PlaylistInfo.MarkerFileName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(fileName, PlaylistInfo.CoverFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string extension = Path.GetExtension(filePath);
                    if (!IsPlaylistAudioExtension(extension))
                    {
                        // 包含非音频文件（且不是标记/封面），认为不是纯粹的歌单目录
                        return false;
                    }
                    hasAudio = true;
                }

                return !requireAudioFiles || hasAudio;
            }
            catch
            {
                return false;
            }
        }

        private bool IsPlaylistAudioExtension(string extension)
        {
            return string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".flac", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".ncm", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsPathUnderDirectory(string path, string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            string normalizedPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedDirectory = Path.GetFullPath(directoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(normalizedPath, normalizedDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string directoryWithSeparator = normalizedDirectory + Path.DirectorySeparatorChar;
            return normalizedPath.StartsWith(directoryWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class PlaylistStorageState
        {
            public string BaseDirectory { get; set; } = string.Empty;

            public List<string> PlaylistPaths { get; set; } = new List<string>();
        }

        private bool CanPlayPreviousSong()
        {
            var playlist = CurrentPlaylist;
            if (playlist == null || CurrentSong == null) return false;
            var songs = playlist.Songs.ToList();
            int currentIndex = songs.IndexOf(CurrentSong);
            return currentIndex > 0;
        }

        private void PlayPreviousSong()
        {
            var playlist = CurrentPlaylist;
            if (playlist == null || CurrentSong == null) return;
            
            var songs = playlist.Songs.ToList();
            int currentIndex = songs.IndexOf(CurrentSong);
            
            if (currentIndex > 0)
            {
                PlaySong(songs[currentIndex - 1]);
            }
        }

        private bool CanPlayNextSong()
        {
            var playlist = CurrentPlaylist;
            if (playlist == null || CurrentSong == null) return false;
            var songs = playlist.Songs.ToList();
            int currentIndex = songs.IndexOf(CurrentSong);
            return currentIndex < songs.Count - 1;
        }

        private void PlayNextSong()
        {
            var playlist = CurrentPlaylist;
            if (playlist == null || CurrentSong == null) return;
            
            var songs = playlist.Songs.ToList();
            int currentIndex = songs.IndexOf(CurrentSong);
            
            if (currentIndex < songs.Count - 1)
            {
                PlaySong(songs[currentIndex + 1]);
            }
        }

        #endregion
    }
}
