using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Musicbox.Models;
using Musicbox.Services;

namespace Musicbox.ViewModels.Pages;

public partial class DownloadPageViewModel : ViewModelBase
{
    private readonly StorageService _storageService;
    private readonly NeteaseDownloadService _downloadService;
    private readonly PlaylistPageViewModel _playlistPageViewModel;
    private CancellationTokenSource? _downloadCts;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string songId = string.Empty;

    [ObservableProperty]
    private string searchKeyword = string.Empty;

    [ObservableProperty]
    private DownloadFormatOption? selectedFormatOption;

    [ObservableProperty]
    private bool importToPlaylistAfterDownload = true;

    [ObservableProperty]
    private PlaylistInfo? selectedImportPlaylist;

    [ObservableProperty]
    private string newPlaylistName = string.Empty;

    [ObservableProperty]
    private string outputPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        "Musicbox",
        "Downloads");

    [ObservableProperty]
    private int downloadProgress;

    [ObservableProperty]
    private string statusText = "准备就绪";

    [ObservableProperty]
    private bool isDownloading;

    [ObservableProperty]
    private bool isSearching;

    [ObservableProperty]
    private SongSearchResult? selectedSearchResult;

    public ObservableCollection<DownloadTaskInfo> DownloadTasks { get; } = [];

    public ObservableCollection<SongSearchResult> SearchResults { get; } = [];

    public ObservableCollection<PlaylistInfo> AvailablePlaylists => _playlistPageViewModel.Playlists;

    public IReadOnlyList<DownloadFormatOption> FormatOptions { get; } =
    [
        new() { Format = DownloadAudioFormat.Mp3, DisplayName = "MP3（推荐）" },
        new() { Format = DownloadAudioFormat.Flac, DisplayName = "FLAC 无损" }
    ];

    public bool HasAvailablePlaylists => AvailablePlaylists.Count > 0;

    public IAsyncRelayCommand BrowseOutputCommand { get; }

    public IAsyncRelayCommand StartDownloadCommand { get; }

    public IAsyncRelayCommand SearchSongsCommand { get; }

    public IAsyncRelayCommand DownloadSelectedSongCommand { get; }

    public IRelayCommand CancelDownloadCommand { get; }

    public IRelayCommand<DownloadTaskInfo> ImportTaskToPlaylistCommand { get; }

    public DownloadPageViewModel(
        StorageService storageService,
        NeteaseDownloadService downloadService,
        PlaylistPageViewModel playlistPageViewModel)
    {
        _storageService = storageService;
        _downloadService = downloadService;
        _playlistPageViewModel = playlistPageViewModel;

        Directory.CreateDirectory(OutputPath);

        BrowseOutputCommand = new AsyncRelayCommand(BrowseOutputAsync);
        StartDownloadCommand = new AsyncRelayCommand(StartDownloadAsync, CanStartDownload);
        SearchSongsCommand = new AsyncRelayCommand(SearchSongsAsync, CanSearchSongs);
        DownloadSelectedSongCommand = new AsyncRelayCommand(DownloadSelectedSongAsync, CanDownloadSelectedSong);
        CancelDownloadCommand = new RelayCommand(CancelDownload, () => IsDownloading);
        ImportTaskToPlaylistCommand = new RelayCommand<DownloadTaskInfo>(ImportTaskToPlaylist, CanImportTask);

        SelectedFormatOption = FormatOptions[0];
        SelectedImportPlaylist = _playlistPageViewModel.SelectedPlaylist ?? AvailablePlaylists.FirstOrDefault();

        AvailablePlaylists.CollectionChanged += OnAvailablePlaylistsChanged;
        _playlistPageViewModel.PropertyChanged += OnPlaylistPagePropertyChanged;
    }

    partial void OnSongIdChanged(string value) => NotifyCommandState();

    partial void OnSearchKeywordChanged(string value) => NotifyCommandState();

    partial void OnIsDownloadingChanged(bool value) => NotifyCommandState();

    partial void OnIsSearchingChanged(bool value) => NotifyCommandState();

    partial void OnOutputPathChanged(string value) => NotifyCommandState();

    partial void OnImportToPlaylistAfterDownloadChanged(bool value) => NotifyCommandState();

    partial void OnSelectedImportPlaylistChanged(PlaylistInfo? value) => NotifyCommandState();

    partial void OnNewPlaylistNameChanged(string value) => NotifyCommandState();

    partial void OnSelectedSearchResultChanged(SongSearchResult? value)
    {
        if (value is not null)
        {
            SongId = value.SongId;
        }

        NotifyCommandState();
    }

    private void OnAvailablePlaylistsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasAvailablePlaylists));
        SelectedImportPlaylist ??= AvailablePlaylists.FirstOrDefault();
        NotifyCommandState();
    }

    private void OnPlaylistPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaylistPageViewModel.SelectedPlaylist) &&
            _playlistPageViewModel.SelectedPlaylist is not null)
        {
            SelectedImportPlaylist = _playlistPageViewModel.SelectedPlaylist;
        }
    }

    private async Task BrowseOutputAsync()
    {
        var folder = await _storageService.PickFolderAsync("选择下载保存目录");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            OutputPath = folder;
            Directory.CreateDirectory(folder);
            StatusText = $"下载目录已切换到 {folder}";
        }
    }

    private bool CanStartDownload()
    {
        return !IsDownloading &&
               _downloadService.IsValidSongId(SongId) &&
               !string.IsNullOrWhiteSpace(OutputPath);
    }

    private bool CanSearchSongs()
    {
        return !IsSearching && !string.IsNullOrWhiteSpace(SearchKeyword);
    }

    private async Task SearchSongsAsync()
    {
        if (!CanSearchSongs())
        {
            return;
        }

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        IsSearching = true;
        StatusText = $"正在搜索: {SearchKeyword.Trim()}";
        SearchResults.Clear();
        SelectedSearchResult = null;

        try
        {
            var results = await _downloadService.SearchSongsAsync(SearchKeyword, 20, _searchCts.Token);
            foreach (var result in results)
            {
                SearchResults.Add(result);
            }

            StatusText = SearchResults.Count > 0
                ? $"搜索完成，找到 {SearchResults.Count} 首歌曲，选中后可下载"
                : "没有搜索到可用歌曲，请尝试更换关键词";

            if (SearchResults.Count > 0)
            {
                SelectedSearchResult = SearchResults[0];
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "搜索已取消";
        }
        catch (Exception ex)
        {
            StatusText = $"搜索失败: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
            _searchCts?.Dispose();
            _searchCts = null;
        }
    }

    private async Task StartDownloadAsync()
    {
        if (!CanStartDownload())
        {
            return;
        }

        Directory.CreateDirectory(OutputPath);
        _downloadCts = new CancellationTokenSource();
        IsDownloading = true;
        DownloadProgress = 0;

        var task = new DownloadTaskInfo
        {
            SongId = SongId
        };
        DownloadTasks.Insert(0, task);

        var progress = new Progress<DownloadProgress>(value =>
        {
            DownloadProgress = value.Progress;
            StatusText = $"{value.Phase} ({value.Progress}%)";
        });

        try
        {
            var result = await _downloadService.DownloadAsync(
                task,
                OutputPath,
                SelectedFormatOption?.Format ?? DownloadAudioFormat.Mp3,
                progress,
                _downloadCts.Token);

            if (result.Success)
            {
                StatusText = BuildDownloadSuccessStatus(result);
            }
            else
            {
                StatusText = result.Cancelled
                    ? "下载已取消"
                    : $"下载失败: {result.ErrorMessage}";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "下载已取消";
        }
        catch (Exception ex)
        {
            StatusText = $"下载失败: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
            ImportTaskToPlaylistCommand.NotifyCanExecuteChanged();
        }
    }

    private string BuildDownloadSuccessStatus(DownloadResult result)
    {
        if (!ImportToPlaylistAfterDownload || string.IsNullOrWhiteSpace(result.OutputPath))
        {
            return $"下载完成: {result.FileName}";
        }

        if (TryImportDownloadedFile(result.OutputPath, out var importMessage, out var playlistName))
        {
            return $"下载完成并已导入歌单「{playlistName}」: {result.FileName}";
        }

        return $"下载完成: {result.FileName}（导入歌单失败: {importMessage}）";
    }

    private bool CanDownloadSelectedSong()
    {
        return !IsDownloading &&
               SelectedSearchResult is not null &&
               !string.IsNullOrWhiteSpace(OutputPath);
    }

    private async Task DownloadSelectedSongAsync()
    {
        if (SelectedSearchResult is null)
        {
            return;
        }

        SongId = SelectedSearchResult.SongId;
        await StartDownloadAsync();
    }

    private void CancelDownload()
    {
        _downloadCts?.Cancel();
        StatusText = "正在取消...";
    }

    private bool CanImportTask(DownloadTaskInfo? task)
    {
        return task?.Status == DownloadStatus.Completed &&
               !string.IsNullOrWhiteSpace(task.OutputPath) &&
               File.Exists(task.OutputPath);
    }

    private void ImportTaskToPlaylist(DownloadTaskInfo? task)
    {
        if (task is null || string.IsNullOrWhiteSpace(task.OutputPath))
        {
            return;
        }

        if (TryImportDownloadedFile(task.OutputPath, out var message, out var playlistName))
        {
            StatusText = $"已导入歌单「{playlistName}」: {Path.GetFileName(task.OutputPath)}";
        }
        else
        {
            StatusText = message;
        }
    }

    private bool TryImportDownloadedFile(string filePath, out string message, out string playlistName)
    {
        playlistName = string.Empty;

        if (!File.Exists(filePath))
        {
            message = "音频文件不存在";
            return false;
        }

        var targetPlaylist = ResolveImportPlaylist(out message);
        if (targetPlaylist is null)
        {
            return false;
        }

        playlistName = targetPlaylist.Name;
        _playlistPageViewModel.SelectedPlaylist = targetPlaylist;
        SelectedImportPlaylist = targetPlaylist;

        var addedCount = _playlistPageViewModel.AddFilesToPlaylist(targetPlaylist, [filePath], out message);
        return addedCount > 0;
    }

    private PlaylistInfo? ResolveImportPlaylist(out string message)
    {
        message = string.Empty;

        if (!string.IsNullOrWhiteSpace(NewPlaylistName))
        {
            if (_playlistPageViewModel.TryCreatePlaylistFromInput(NewPlaylistName, null, out var createdPlaylist, out var createMessage))
            {
                NewPlaylistName = string.Empty;
                return createdPlaylist;
            }

            message = createMessage;
            return null;
        }

        if (SelectedImportPlaylist is not null)
        {
            return SelectedImportPlaylist;
        }

        if (_playlistPageViewModel.SelectedPlaylist is not null)
        {
            return _playlistPageViewModel.SelectedPlaylist;
        }

        message = "请先选择目标歌单，或输入新歌单名称";
        return null;
    }

    private void NotifyCommandState()
    {
        StartDownloadCommand?.NotifyCanExecuteChanged();
        SearchSongsCommand?.NotifyCanExecuteChanged();
        DownloadSelectedSongCommand?.NotifyCanExecuteChanged();
        CancelDownloadCommand?.NotifyCanExecuteChanged();
        ImportTaskToPlaylistCommand?.NotifyCanExecuteChanged();
    }
}
