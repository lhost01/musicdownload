using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Musicbox.Models;
using Musicbox.Services;

namespace Musicbox.ViewModels.Pages;

public partial class ConvertPageViewModel : ViewModelBase
{
    private readonly StorageService _storageService;
    private readonly AudioConverterService _audioConverterService;
    private readonly PlaylistPageViewModel _playlistPageViewModel;
    private CancellationTokenSource? _conversionCts;

    [ObservableProperty]
    private InputMode inputMode = InputMode.SingleFile;

    [ObservableProperty]
    private string inputPath = string.Empty;

    [ObservableProperty]
    private string outputPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        "Musicbox",
        "Converted");

    [ObservableProperty]
    private string outputFormat = "mp3";

    [ObservableProperty]
    private int conversionProgress;

    [ObservableProperty]
    private string currentConvertingFile = "等待开始";

    [ObservableProperty]
    private string conversionStatusText = "准备就绪";

    [ObservableProperty]
    private bool isConverting;

    [ObservableProperty]
    private NcmFileInfo? selectedFile;

    [ObservableProperty]
    private bool showImportToPlaylistOptions;

    [ObservableProperty]
    private PlaylistInfo? selectedImportPlaylist;

    [ObservableProperty]
    private string newPlaylistName = string.Empty;

    [ObservableProperty]
    private string importStatusText = "转换完成后可将歌曲导入歌单";

    public ObservableCollection<string> SupportedFormats { get; } = [];

    public ObservableCollection<NcmFileInfo> NcmFiles { get; } = [];

    public ObservableCollection<string> ConvertedFiles { get; } = [];

    public ObservableCollection<PlaylistInfo> AvailablePlaylists => _playlistPageViewModel.Playlists;

    public bool HasAvailablePlaylists => AvailablePlaylists.Count > 0;

    public string ImportSummaryText => ConvertedFiles.Count == 0
        ? "当前还没有可导入的转换结果"
        : $"本次成功转换 {ConvertedFiles.Count} 首，可导入现有歌单或新歌单";

    public IAsyncRelayCommand BrowseInputCommand { get; }

    public IAsyncRelayCommand BrowseOutputCommand { get; }

    public IRelayCommand SetSingleFileModeCommand { get; }

    public IRelayCommand SetBatchModeCommand { get; }

    public IRelayCommand SetMultipleFilesModeCommand { get; }

    public IAsyncRelayCommand StartConversionCommand { get; }

    public IRelayCommand CancelConversionCommand { get; }

    public IRelayCommand<NcmFileInfo> RemoveFileCommand { get; }

    public IRelayCommand ClearFilesCommand { get; }

    public IRelayCommand DeleteSelectedFileCommand { get; }

    public IRelayCommand ImportToExistingPlaylistCommand { get; }

    public IRelayCommand ImportToNewPlaylistCommand { get; }

    public IRelayCommand SkipImportToPlaylistCommand { get; }

    public ConvertPageViewModel(
        StorageService storageService,
        AudioConverterService audioConverterService,
        PlaylistPageViewModel playlistPageViewModel)
    {
        _storageService = storageService;
        _audioConverterService = audioConverterService;
        _playlistPageViewModel = playlistPageViewModel;

        foreach (var format in _audioConverterService.SupportedFormats)
        {
            SupportedFormats.Add(format);
        }

        Directory.CreateDirectory(OutputPath);

        BrowseInputCommand = new AsyncRelayCommand(BrowseInputAsync);
        BrowseOutputCommand = new AsyncRelayCommand(BrowseOutputAsync);
        SetSingleFileModeCommand = new RelayCommand(() => InputMode = InputMode.SingleFile);
        SetBatchModeCommand = new RelayCommand(() => InputMode = InputMode.BatchFolder);
        SetMultipleFilesModeCommand = new RelayCommand(() => InputMode = InputMode.MultipleFiles);
        StartConversionCommand = new AsyncRelayCommand(StartConversionAsync, CanStartConversion);
        CancelConversionCommand = new RelayCommand(CancelConversion, () => IsConverting);
        RemoveFileCommand = new RelayCommand<NcmFileInfo>(RemoveFile);
        ClearFilesCommand = new RelayCommand(ClearFiles, () => NcmFiles.Count > 0);
        DeleteSelectedFileCommand = new RelayCommand(() => RemoveFile(SelectedFile), () => SelectedFile is not null);
        ImportToExistingPlaylistCommand = new RelayCommand(ImportToExistingPlaylist, CanImportToExistingPlaylist);
        ImportToNewPlaylistCommand = new RelayCommand(ImportToNewPlaylist, CanImportToNewPlaylist);
        SkipImportToPlaylistCommand = new RelayCommand(SkipImportToPlaylist, () => ShowImportToPlaylistOptions);

        ConvertedFiles.CollectionChanged += OnConvertedFilesChanged;
        AvailablePlaylists.CollectionChanged += OnAvailablePlaylistsChanged;
        SelectedImportPlaylist = AvailablePlaylists.FirstOrDefault();
    }

    public bool IsSingleFileMode => InputMode == InputMode.SingleFile;

    public bool IsBatchMode => InputMode == InputMode.BatchFolder;

    public bool IsMultipleFilesMode => InputMode == InputMode.MultipleFiles;

    partial void OnInputModeChanged(InputMode value)
    {
        InputPath = string.Empty;
        NcmFiles.Clear();
        OnPropertyChanged(nameof(IsSingleFileMode));
        OnPropertyChanged(nameof(IsBatchMode));
        OnPropertyChanged(nameof(IsMultipleFilesMode));
        NotifyCommandState();
    }

    partial void OnIsConvertingChanged(bool value) => NotifyCommandState();

    partial void OnOutputPathChanged(string value) => NotifyCommandState();

    partial void OnSelectedFileChanged(NcmFileInfo? value) => NotifyCommandState();

    partial void OnSelectedImportPlaylistChanged(PlaylistInfo? value) => NotifyCommandState();

    partial void OnNewPlaylistNameChanged(string value) => NotifyCommandState();

    partial void OnShowImportToPlaylistOptionsChanged(bool value) => NotifyCommandState();

    private async Task BrowseInputAsync()
    {
        switch (InputMode)
        {
            case InputMode.SingleFile:
                await BrowseSingleFileAsync();
                break;
            case InputMode.BatchFolder:
                await BrowseFolderAsync();
                break;
            case InputMode.MultipleFiles:
                await BrowseMultipleFilesAsync();
                break;
        }
    }

    private async Task BrowseSingleFileAsync()
    {
        var files = await _storageService.PickNcmFilesAsync(false);
        var filePath = files.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        NcmFiles.Clear();
        if (_audioConverterService.IsValidNcmFile(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            NcmFiles.Add(new NcmFileInfo
            {
                FileName = fileInfo.Name,
                FullPath = fileInfo.FullName,
                FileSize = fileInfo.Length
            });
            InputPath = filePath;
            ConversionStatusText = "已选择 1 个文件";
        }
        else
        {
            ConversionStatusText = "选择的文件不是有效 NCM";
        }

        NotifyCommandState();
    }

    private async Task BrowseMultipleFilesAsync()
    {
        var files = await _storageService.PickNcmFilesAsync(true);
        if (files.Count == 0)
        {
            return;
        }

        NcmFiles.Clear();
        foreach (var filePath in files.Where(_audioConverterService.IsValidNcmFile))
        {
            var fileInfo = new FileInfo(filePath);
            NcmFiles.Add(new NcmFileInfo
            {
                FileName = fileInfo.Name,
                FullPath = fileInfo.FullName,
                FileSize = fileInfo.Length
            });
        }

        InputPath = $"已选择 {NcmFiles.Count} 个文件";
        ConversionStatusText = $"找到 {NcmFiles.Count} 个有效 NCM 文件";
        NotifyCommandState();
    }

    private async Task BrowseFolderAsync()
    {
        var folder = await _storageService.PickFolderAsync("选择包含 NCM 文件的文件夹");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        InputPath = folder;
        NcmFiles.Clear();
        foreach (var file in _audioConverterService.ScanNcmFiles(folder, SearchOption.TopDirectoryOnly))
        {
            NcmFiles.Add(file);
        }

        ConversionStatusText = $"找到 {NcmFiles.Count} 个 NCM 文件";
        NotifyCommandState();
    }

    private async Task BrowseOutputAsync()
    {
        var folder = await _storageService.PickFolderAsync("选择输出文件夹");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            OutputPath = folder;
            Directory.CreateDirectory(folder);
            ConversionStatusText = $"输出目录已切换到 {folder}";
        }
    }

    private bool CanStartConversion()
    {
        return !IsConverting &&
               NcmFiles.Count > 0 &&
               !string.IsNullOrWhiteSpace(OutputPath);
    }

    private async Task StartConversionAsync()
    {
        if (!CanStartConversion())
        {
            return;
        }

        Directory.CreateDirectory(OutputPath);
        _conversionCts = new CancellationTokenSource();
        IsConverting = true;
        ConversionProgress = 0;
        ConvertedFiles.Clear();
        ShowImportToPlaylistOptions = false;
        NewPlaylistName = string.Empty;
        ImportStatusText = "正在准备转换结果";

        var progress = new Progress<BatchConversionProgress>(value =>
        {
            CurrentConvertingFile = value.CurrentFile;
            ConversionProgress = value.OverallProgress;
            ConversionStatusText = $"正在转换: {value.CurrentFile} ({value.CurrentFileProgress}%)";
        });

        try
        {
            var result = await _audioConverterService.ConvertBatchAsync(
                NcmFiles.ToList(),
                OutputFormat,
                OutputPath,
                progress,
                _conversionCts.Token);

            foreach (var file in result.ConvertedFilePaths)
            {
                ConvertedFiles.Add(file);
            }

            ConversionStatusText = $"转换完成: {result.CompletedFiles} 成功, {result.FailedFiles} 失败, {result.CancelledFiles} 取消";
            ConversionProgress = 100;
            CurrentConvertingFile = "本次任务已完成";
            if (ConvertedFiles.Count > 0)
            {
                SelectedImportPlaylist = AvailablePlaylists.FirstOrDefault();
                ShowImportToPlaylistOptions = true;
                ImportStatusText = HasAvailablePlaylists
                    ? "转换完成，可直接导入现有歌单，或创建新歌单后导入"
                    : "转换完成，当前还没有歌单，可直接创建新歌单导入";
            }
            else
            {
                ImportStatusText = "没有可导入的转换结果";
            }
        }
        catch (OperationCanceledException)
        {
            ConversionStatusText = "转换已取消";
            ImportStatusText = "转换已取消";
        }
        catch (Exception ex)
        {
            ConversionStatusText = $"转换失败: {ex.Message}";
            ImportStatusText = "转换失败，无法导入歌单";
        }
        finally
        {
            IsConverting = false;
            _conversionCts?.Dispose();
            _conversionCts = null;
        }
    }

    private void CancelConversion()
    {
        _conversionCts?.Cancel();
        ConversionStatusText = "正在取消...";
    }

    private void RemoveFile(NcmFileInfo? file)
    {
        if (file is null)
        {
            return;
        }

        NcmFiles.Remove(file);
        NotifyCommandState();
    }

    private void ClearFiles()
    {
        NcmFiles.Clear();
        InputPath = string.Empty;
        NotifyCommandState();
    }

    private bool CanImportToExistingPlaylist()
    {
        return ShowImportToPlaylistOptions &&
               ConvertedFiles.Count > 0 &&
               SelectedImportPlaylist is not null;
    }

    private void ImportToExistingPlaylist()
    {
        if (SelectedImportPlaylist is null)
        {
            return;
        }

        _playlistPageViewModel.SelectedPlaylist = SelectedImportPlaylist;
        var addedCount = _playlistPageViewModel.AddFilesToPlaylist(SelectedImportPlaylist, ConvertedFiles, out var statusMessage);
        ConversionStatusText = $"已导入 {addedCount} 首歌曲到歌单 {SelectedImportPlaylist.Name}";
        ImportStatusText = statusMessage;
        ShowImportToPlaylistOptions = false;
    }

    private bool CanImportToNewPlaylist()
    {
        return ShowImportToPlaylistOptions &&
               ConvertedFiles.Count > 0 &&
               !string.IsNullOrWhiteSpace(NewPlaylistName);
    }

    private void ImportToNewPlaylist()
    {
        if (!_playlistPageViewModel.TryCreatePlaylistFromInput(NewPlaylistName, null, out var playlist, out var statusMessage))
        {
            ConversionStatusText = statusMessage;
            ImportStatusText = statusMessage;
            return;
        }

        if (playlist is null)
        {
            ConversionStatusText = "创建新歌单失败";
            ImportStatusText = "创建新歌单失败";
            return;
        }

        var addedCount = _playlistPageViewModel.AddFilesToPlaylist(playlist, ConvertedFiles, out var importStatusMessage);
        SelectedImportPlaylist = playlist;
        NewPlaylistName = string.Empty;
        ConversionStatusText = $"已创建歌单 {playlist.Name} 并导入 {addedCount} 首歌曲";
        ImportStatusText = importStatusMessage;
        ShowImportToPlaylistOptions = false;
    }

    private void SkipImportToPlaylist()
    {
        ShowImportToPlaylistOptions = false;
        ImportStatusText = "已跳过本次导入，可稍后在歌单中心手动添加";
    }

    private void NotifyCommandState()
    {
        StartConversionCommand.NotifyCanExecuteChanged();
        CancelConversionCommand.NotifyCanExecuteChanged();
        ClearFilesCommand.NotifyCanExecuteChanged();
        DeleteSelectedFileCommand.NotifyCanExecuteChanged();
        ImportToExistingPlaylistCommand.NotifyCanExecuteChanged();
        ImportToNewPlaylistCommand.NotifyCanExecuteChanged();
        SkipImportToPlaylistCommand.NotifyCanExecuteChanged();
    }

    private void OnConvertedFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ImportSummaryText));
        NotifyCommandState();
    }

    private void OnAvailablePlaylistsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasAvailablePlaylists));
        if (SelectedImportPlaylist is not null && AvailablePlaylists.Contains(SelectedImportPlaylist))
        {
            NotifyCommandState();
            return;
        }

        SelectedImportPlaylist = AvailablePlaylists.FirstOrDefault();
        NotifyCommandState();
    }
}
