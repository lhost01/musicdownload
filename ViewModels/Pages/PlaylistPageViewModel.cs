using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Musicbox.Helpers;
using Musicbox.Models;
using Musicbox.Services;

namespace Musicbox.ViewModels.Pages;

public partial class PlaylistPageViewModel : ViewModelBase
{
    private static readonly Regex LyricTimestampRegex = new(@"\[[^\]]+\]", RegexOptions.Compiled);
    private readonly StorageService _storageService;
    private readonly MusicPlayerService _musicPlayerService;
    private readonly string _configPath;
    private bool _isSyncingPosition;
    private double _currentPositionSeconds;

    [ObservableProperty]
    private string baseDirectory;

    [ObservableProperty]
    private string createPlaylistName = string.Empty;

    [ObservableProperty]
    private string createCoverPath = string.Empty;

    [ObservableProperty]
    private string playlistDraftName = string.Empty;

    [ObservableProperty]
    private string newCoverPath = string.Empty;

    [ObservableProperty]
    private string searchKeyword = string.Empty;

    [ObservableProperty]
    private string statusText = "准备就绪";

    [ObservableProperty]
    private PlaylistInfo? selectedPlaylist;

    [ObservableProperty]
    private PlaylistSongInfo? selectedSong;

    [ObservableProperty]
    private PlaylistSongInfo? currentSong;

    [ObservableProperty]
    private string currentSongName = "未播放";

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private TimeSpan totalDuration;

    [ObservableProperty]
    private double totalDurationSeconds;

    [ObservableProperty]
    private double volume = 0.75;

    [ObservableProperty]
    private string lyricsSourceText = "播放歌曲后将在这里显示歌词或音乐信息";

    public ObservableCollection<PlaylistInfo> Playlists { get; } = [];

    public ObservableCollection<PlaylistInfo> FilteredPlaylists { get; } = [];

    public ObservableCollection<string> CurrentLyrics { get; } = [];

    public double CurrentPositionSeconds
    {
        get => _currentPositionSeconds;
        set
        {
            if (SetProperty(ref _currentPositionSeconds, value))
            {
                OnPropertyChanged(nameof(CurrentPositionText));
                if (!_isSyncingPosition)
                {
                    _musicPlayerService.SetPosition(TimeSpan.FromSeconds(value));
                }
            }
        }
    }

    public IAsyncRelayCommand BrowseBaseDirectoryCommand { get; }

    public IRelayCommand RefreshCommand { get; }

    public IRelayCommand CreatePlaylistCommand { get; }

    public IAsyncRelayCommand BrowseCreateCoverCommand { get; }

    public IRelayCommand ClearCreateCoverCommand { get; }

    public IRelayCommand RenamePlaylistCommand { get; }

    public IRelayCommand DeletePlaylistCommand { get; }

    public IAsyncRelayCommand BrowseCoverCommand { get; }

    public IRelayCommand ApplyCoverCommand { get; }

    public IRelayCommand RemoveCoverCommand { get; }

    public IAsyncRelayCommand AddSongsCommand { get; }

    public IRelayCommand<PlaylistSongInfo> RemoveSongCommand { get; }

    public IRelayCommand DeleteSelectedSongCommand { get; }

    public IRelayCommand OpenFolderCommand { get; }

    public IRelayCommand<PlaylistSongInfo> PlaySongCommand { get; }

    public IRelayCommand TogglePlaybackCommand { get; }

    public IRelayCommand PreviousSongCommand { get; }

    public IRelayCommand NextSongCommand { get; }

    public string PlaylistCollectionSummary => $"{Playlists.Count} 张歌单 · {Playlists.Sum(item => item.SongCount)} 首歌曲";

    public string SelectedPlaylistDisplayName => SelectedPlaylist?.Name ?? "还没有选中歌单";

    public string SelectedPlaylistStatText => SelectedPlaylist is null
        ? "创建歌单后即可搭建你的专属音乐空间"
        : $"{SelectedPlaylist.SongCountText} · 创建于 {SelectedPlaylist.CreatedTimeText}";

    public string SelectedPlaylistRuntimeText => SelectedPlaylist is null
        ? "总时长 00:00"
        : $"总时长 {FormatTime(TimeSpan.FromTicks(SelectedPlaylist.Songs.Sum(song => song.Duration.Ticks)))}";

    public string SelectedPlaylistDirectoryText => SelectedPlaylist?.FolderPath ?? "当前还没有可用歌单";

    public Bitmap? SelectedPlaylistCoverBitmap => SelectedPlaylist?.CoverBitmap;

    public string SelectedPlaylistInitial => string.IsNullOrWhiteSpace(SelectedPlaylist?.Name)
        ? "M"
        : SelectedPlaylist.Name[..1].ToUpperInvariant();

    public bool HasSelectedPlaylist => SelectedPlaylist is not null;

    public string SelectedPlaylistEditorTitle => SelectedPlaylist is null ? "创建新歌单" : $"编辑歌单 · {SelectedPlaylist.Name}";

    public string SelectedPlaylistEditorHint => SelectedPlaylist is null
        ? "先从右侧歌单列表选择一个歌单，再修改名称或封面。"
        : "可直接修改歌单名，或重新选择一张封面后点击应用。";

    public string CreatePendingCoverText => string.IsNullOrWhiteSpace(CreateCoverPath)
        ? "新歌单未设置封面，将使用默认样式"
        : $"新歌单封面: {Path.GetFileName(CreateCoverPath)}";

    public Bitmap? CreatePendingCoverPreviewBitmap => ImageSourceHelper.LoadBitmap(CreateCoverPath);

    public string PendingCoverText => string.IsNullOrWhiteSpace(NewCoverPath)
        ? "还没有选择新的封面图片"
        : $"待应用封面: {Path.GetFileName(NewCoverPath)}";

    public Bitmap? PendingCoverPreviewBitmap => ImageSourceHelper.LoadBitmap(NewCoverPath);

    public string SearchResultText => string.IsNullOrWhiteSpace(SearchKeyword)
        ? $"显示全部 {FilteredPlaylists.Count} 张歌单"
        : $"搜索到 {FilteredPlaylists.Count} 张歌单";

    public string CurrentSongSubtitle
    {
        get
        {
            if (CurrentSong is null)
            {
                return "从歌曲列表中选择一首歌，底部悬浮播放条会跟随更新";
            }

            return string.IsNullOrWhiteSpace(CurrentSong.ArtistAlbumText)
                ? "本地音频"
                : CurrentSong.ArtistAlbumText;
        }
    }

    public string PlaybackStateText => CurrentSong is null
        ? "待机中"
        : IsPlaying ? "正在播放" : "已暂停";

    public string CurrentPositionText => FormatTime(TimeSpan.FromSeconds(CurrentPositionSeconds));

    public string TotalDurationText => FormatTime(TotalDuration);

    public string VolumePercentText => $"{Volume * 100:F0}%";

    public PlaylistPageViewModel(StorageService storageService, MusicPlayerService musicPlayerService)
    {
        _storageService = storageService;
        _musicPlayerService = musicPlayerService;

        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Musicbox",
            "playlist-base.txt");

        BaseDirectory = LoadBaseDirectory();

        BrowseBaseDirectoryCommand = new AsyncRelayCommand(BrowseBaseDirectoryAsync);
        RefreshCommand = new RelayCommand(LoadPlaylists);
        CreatePlaylistCommand = new RelayCommand(CreatePlaylist, CanCreatePlaylist);
        BrowseCreateCoverCommand = new AsyncRelayCommand(BrowseCreateCoverAsync);
        ClearCreateCoverCommand = new RelayCommand(ClearCreateCover, () => !string.IsNullOrWhiteSpace(CreateCoverPath));
        RenamePlaylistCommand = new RelayCommand(RenamePlaylist, CanRenamePlaylist);
        DeletePlaylistCommand = new RelayCommand(DeletePlaylist, () => SelectedPlaylist is not null);
        BrowseCoverCommand = new AsyncRelayCommand(BrowseCoverAsync, () => SelectedPlaylist is not null);
        ApplyCoverCommand = new RelayCommand(ApplyCover, () => SelectedPlaylist is not null && File.Exists(NewCoverPath));
        RemoveCoverCommand = new RelayCommand(RemoveCover, () => SelectedPlaylist?.HasCustomCover == true);
        AddSongsCommand = new AsyncRelayCommand(AddSongsAsync, () => SelectedPlaylist is not null);
        RemoveSongCommand = new RelayCommand<PlaylistSongInfo>(RemoveSong);
        DeleteSelectedSongCommand = new RelayCommand(() => RemoveSong(SelectedSong), () => SelectedSong is not null);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => SelectedPlaylist is not null);
        PlaySongCommand = new RelayCommand<PlaylistSongInfo>(PlaySong);
        TogglePlaybackCommand = new RelayCommand(TogglePlayback, () => CurrentSong is not null);
        PreviousSongCommand = new RelayCommand(PlayPrevious, CanPlayPrevious);
        NextSongCommand = new RelayCommand(PlayNext, CanPlayNext);

        Directory.CreateDirectory(BaseDirectory);
        InitializePlayerEvents();
        LoadPlaylists();
        LoadLyricsForSong(null);
    }

    partial void OnSelectedPlaylistChanged(PlaylistInfo? value)
    {
        value?.ReloadSongs();
        PlaylistDraftName = value?.Name ?? string.Empty;
        NewCoverPath = string.Empty;
        StatusText = value is null ? "未选择歌单" : $"已切换到歌单: {value.Name}";
        RefreshPlaylistDashboard();
        NotifyCommandState();
    }

    partial void OnCurrentSongChanged(PlaylistSongInfo? value)
    {
        CurrentSongName = value?.DisplayName ?? "未播放";
        LoadLyricsForSong(value);
        OnPropertyChanged(nameof(CurrentSongSubtitle));
        OnPropertyChanged(nameof(PlaybackStateText));
        NotifyCommandState();
    }

    partial void OnVolumeChanged(double value)
    {
        _musicPlayerService.SetVolume(value);
        OnPropertyChanged(nameof(VolumePercentText));
    }

    partial void OnCreatePlaylistNameChanged(string value) => NotifyCommandState();

    partial void OnCreateCoverPathChanged(string value)
    {
        OnPropertyChanged(nameof(CreatePendingCoverText));
        OnPropertyChanged(nameof(CreatePendingCoverPreviewBitmap));
        NotifyCommandState();
    }

    partial void OnPlaylistDraftNameChanged(string value) => NotifyCommandState();

    partial void OnNewCoverPathChanged(string value)
    {
        OnPropertyChanged(nameof(PendingCoverText));
        OnPropertyChanged(nameof(PendingCoverPreviewBitmap));
        NotifyCommandState();
    }

    partial void OnSearchKeywordChanged(string value)
    {
        RefreshFilteredPlaylists();
        OnPropertyChanged(nameof(SearchResultText));
    }

    partial void OnSelectedSongChanged(PlaylistSongInfo? value) => NotifyCommandState();

    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(PlaybackStateText));

    partial void OnTotalDurationChanged(TimeSpan value) => OnPropertyChanged(nameof(TotalDurationText));

    private void InitializePlayerEvents()
    {
        _musicPlayerService.PositionChanged += (_, position) =>
        {
            _isSyncingPosition = true;
            CurrentPositionSeconds = position.TotalSeconds;
            _isSyncingPosition = false;
        };

        _musicPlayerService.DurationChanged += (_, duration) =>
        {
            TotalDuration = duration;
            TotalDurationSeconds = duration.TotalSeconds;
        };

        _musicPlayerService.PlaybackStarted += (_, _) =>
        {
            IsPlaying = true;
            OnPropertyChanged(nameof(CurrentSongSubtitle));
        };

        _musicPlayerService.PlaybackPaused += (_, _) =>
        {
            IsPlaying = false;
        };

        _musicPlayerService.PlaybackStopped += (_, _) =>
        {
            IsPlaying = false;
        };

        _musicPlayerService.PlaybackCompleted += (_, _) =>
        {
            IsPlaying = false;
            PlayNext();
        };
    }

    private async Task BrowseBaseDirectoryAsync()
    {
        var folder = await _storageService.PickFolderAsync("选择歌单存储目录");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        BaseDirectory = folder;
        Directory.CreateDirectory(BaseDirectory);
        SaveBaseDirectory();
        LoadPlaylists();
        StatusText = $"歌单目录已切换到 {BaseDirectory}";
    }

    private void LoadPlaylists()
    {
        Playlists.Clear();
        if (!Directory.Exists(BaseDirectory))
        {
            RefreshPlaylistDashboard();
            return;
        }

        foreach (var folder in Directory.GetDirectories(BaseDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (!PlaylistInfo.IsPlaylistFolder(folder))
            {
                continue;
            }

            var playlist = new PlaylistInfo
            {
                Name = Path.GetFileName(folder),
                FolderPath = folder,
                CreatedTime = Directory.GetCreationTime(folder)
            };
            playlist.ReloadSongs();
            Playlists.Add(playlist);
        }

        SelectedPlaylist = null;
        RefreshFilteredPlaylists();
        RefreshPlaylistDashboard();
        NotifyCommandState();
    }

    private bool CanCreatePlaylist()
    {
        return !string.IsNullOrWhiteSpace(CreatePlaylistName);
    }

    private void CreatePlaylist()
    {
        if (TryCreatePlaylistFromInput(CreatePlaylistName, CreateCoverPath, out _, out var statusMessage))
        {
            CreatePlaylistName = string.Empty;
            CreateCoverPath = string.Empty;
        }

        StatusText = statusMessage;
    }

    public bool TryCreatePlaylistFromInput(string rawName, string? coverPath, out PlaylistInfo? playlist, out string statusMessage)
    {
        playlist = null;

        var name = rawName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            statusMessage = "请输入歌单名称";
            return false;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            statusMessage = "歌单名称包含非法字符";
            return false;
        }

        var folderPath = Path.Combine(BaseDirectory, name);
        if (Directory.Exists(folderPath))
        {
            statusMessage = "同名歌单已存在";
            return false;
        }

        if (!PlaylistInfo.TryEnsureMarker(folderPath, out var markerError))
        {
            statusMessage = markerError ?? "创建歌单失败";
            return false;
        }

        playlist = new PlaylistInfo
        {
            Name = name,
            FolderPath = folderPath,
            CreatedTime = DateTime.Now
        };

        if (!TryApplyCoverToPlaylist(playlist, coverPath, out var coverError))
        {
            statusMessage = coverError ?? $"歌单 {name} 已创建，但封面设置失败";
        }
        else
        {
            statusMessage = string.IsNullOrWhiteSpace(coverPath)
                ? $"歌单 {name} 创建成功"
                : $"歌单 {name} 创建成功并已设置封面";
        }

        Playlists.Add(playlist);
        SelectedPlaylist = playlist;
        RefreshFilteredPlaylists();
        RefreshPlaylistDashboard();
        return true;
    }

    private bool CanRenamePlaylist()
    {
        return SelectedPlaylist is not null &&
               !string.IsNullOrWhiteSpace(PlaylistDraftName) &&
               !string.Equals(SelectedPlaylist.Name, PlaylistDraftName.Trim(), StringComparison.Ordinal);
    }

    private void RenamePlaylist()
    {
        if (SelectedPlaylist is null)
        {
            return;
        }

        var newName = PlaylistDraftName.Trim();
        var newPath = Path.Combine(BaseDirectory, newName);
        if (Directory.Exists(newPath))
        {
            StatusText = "目标名称已存在";
            return;
        }

        try
        {
            // 当前歌单内如果有正在播放的音频，先释放文件句柄再重命名目录。
            if (CurrentSong is not null &&
                string.Equals(Path.GetDirectoryName(CurrentSong.FilePath), SelectedPlaylist.FolderPath, StringComparison.OrdinalIgnoreCase))
            {
                _musicPlayerService.Stop();
                CurrentSong = null;
            }

            Directory.Move(SelectedPlaylist.FolderPath, newPath);
        }
        catch (UnauthorizedAccessException)
        {
            StatusText = $"没有权限重命名歌单目录: {SelectedPlaylist.FolderPath}";
            return;
        }
        catch (IOException)
        {
            StatusText = $"歌单目录正在被占用，暂时无法重命名: {SelectedPlaylist.FolderPath}";
            return;
        }

        var markerUpdated = PlaylistInfo.TryEnsureMarker(newPath, out var markerError);

        SelectedPlaylist.Name = newName;
        SelectedPlaylist.FolderPath = newPath;
        SelectedPlaylist.ReloadSongs();
        StatusText = markerUpdated
            ? $"歌单已重命名为 {newName}"
            : markerError ?? $"歌单已重命名为 {newName}，但标记文件未更新";
        RefreshFilteredPlaylists();
        RefreshPlaylistDashboard();
    }

    private void DeletePlaylist()
    {
        if (SelectedPlaylist is null)
        {
            return;
        }

        var target = SelectedPlaylist;
        if (Directory.Exists(target.FolderPath))
        {
            Directory.Delete(target.FolderPath, true);
        }

        if (CurrentSong is not null && string.Equals(Path.GetDirectoryName(CurrentSong.FilePath), target.FolderPath, StringComparison.OrdinalIgnoreCase))
        {
            _musicPlayerService.Stop();
            CurrentSong = null;
        }

        Playlists.Remove(target);
        SelectedPlaylist = null;
        StatusText = $"歌单 {target.Name} 已删除";
        RefreshFilteredPlaylists();
        RefreshPlaylistDashboard();
    }

    private async Task BrowseCoverAsync()
    {
        if (SelectedPlaylist is null)
        {
            StatusText = "请先选择一个歌单，再选择封面";
            return;
        }

        var filePath = await _storageService.PickImageAsync();
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            NewCoverPath = filePath;
            StatusText = $"已为 {SelectedPlaylist.Name} 选择封面图片";
            NotifyCommandState();
        }
    }

    private async Task BrowseCreateCoverAsync()
    {
        var filePath = await _storageService.PickImageAsync();
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            CreateCoverPath = filePath;
            StatusText = $"已为新歌单选择封面图片: {Path.GetFileName(filePath)}";
        }
    }

    private void ClearCreateCover()
    {
        CreateCoverPath = string.Empty;
        StatusText = "已清空新歌单封面";
    }

    private void ApplyCover()
    {
        if (SelectedPlaylist is null || !File.Exists(NewCoverPath))
        {
            return;
        }

        SelectedPlaylist.CoverImagePath = string.Empty;
        if (!TryApplyCoverToPlaylist(SelectedPlaylist, NewCoverPath, out var errorMessage))
        {
            StatusText = errorMessage ?? "歌单封面更新失败";
            return;
        }

        NewCoverPath = string.Empty;
        StatusText = "歌单封面已更新";
        RefreshFilteredPlaylists();
        RefreshPlaylistDashboard();
        NotifyCommandState();
    }

    private void RemoveCover()
    {
        if (SelectedPlaylist is null)
        {
            return;
        }

        PlaylistInfo.DeleteExistingCoverFiles(SelectedPlaylist.FolderPath);

        SelectedPlaylist.CoverImagePath = string.Empty;
        NewCoverPath = string.Empty;
        StatusText = "歌单封面已移除";
        RefreshFilteredPlaylists();
        RefreshPlaylistDashboard();
        NotifyCommandState();
    }

    private async Task AddSongsAsync()
    {
        if (SelectedPlaylist is null)
        {
            return;
        }

        var files = await _storageService.PickAudioFilesAsync();
        AddFilesToPlaylist(SelectedPlaylist, files, out var statusMessage);
        StatusText = statusMessage;
    }

    public int AddFilesToPlaylist(PlaylistInfo playlist, IEnumerable<string> filePaths, out string statusMessage)
    {
        var addedCount = 0;

        foreach (var filePath in filePaths.Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)))
        {
            try
            {
                var destination = Path.Combine(playlist.FolderPath, Path.GetFileName(filePath));
                if (!File.Exists(destination))
                {
                    File.Copy(filePath, destination);
                }

                var song = PlaylistSongInfo.FromFilePath(destination);
                if (song is not null)
                {
                    playlist.AddSong(song);
                    addedCount++;
                }
            }
            catch
            {
                // 单个文件导入失败时继续处理其它文件，尽量保证批量导入完成。
            }
        }

        if (SelectedPlaylist == playlist)
        {
            RefreshPlaylistDashboard();
            NotifyCommandState();
        }
        else
        {
            OnPropertyChanged(nameof(PlaylistCollectionSummary));
        }

        statusMessage = $"已添加 {addedCount} 首歌曲到歌单 {playlist.Name}";
        return addedCount;
    }

    private void RemoveSong(PlaylistSongInfo? song)
    {
        if (SelectedPlaylist is null || song is null)
        {
            return;
        }

        if (File.Exists(song.FilePath))
        {
            File.Delete(song.FilePath);
        }

        SelectedPlaylist.RemoveSong(song);
        if (CurrentSong == song)
        {
            _musicPlayerService.Stop();
            CurrentSong = null;
        }

        StatusText = $"已移除 {song.DisplayName}";
        RefreshPlaylistDashboard();
        NotifyCommandState();
    }

    private void OpenFolder()
    {
        if (SelectedPlaylist is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{SelectedPlaylist.FolderPath}\"",
            UseShellExecute = true
        });
    }

    private void PlaySong(PlaylistSongInfo? song)
    {
        if (song is null)
        {
            return;
        }

        if (!File.Exists(song.FilePath))
        {
            StatusText = "音频文件不存在";
            return;
        }

        CurrentSong = song;
        CurrentPositionSeconds = 0;
        _musicPlayerService.Play(song.FilePath);
        IsPlaying = true;
        StatusText = $"正在播放: {song.DisplayName}";
        OnPropertyChanged(nameof(CurrentSongSubtitle));
    }

    private void TogglePlayback()
    {
        if (CurrentSong is null)
        {
            return;
        }

        if (IsPlaying)
        {
            _musicPlayerService.Pause();
            IsPlaying = false;
            StatusText = "播放已暂停";
        }
        else
        {
            _musicPlayerService.Resume();
            IsPlaying = true;
            StatusText = $"继续播放: {CurrentSong.DisplayName}";
        }
    }

    private bool CanPlayPrevious()
    {
        if (SelectedPlaylist is null || CurrentSong is null)
        {
            return false;
        }

        var index = SelectedPlaylist.Songs.IndexOf(CurrentSong);
        return index > 0;
    }

    private void PlayPrevious()
    {
        if (SelectedPlaylist is null || CurrentSong is null)
        {
            return;
        }

        var index = SelectedPlaylist.Songs.IndexOf(CurrentSong);
        if (index > 0)
        {
            PlaySong(SelectedPlaylist.Songs[index - 1]);
        }
    }

    private bool CanPlayNext()
    {
        if (SelectedPlaylist is null || CurrentSong is null)
        {
            return false;
        }

        var index = SelectedPlaylist.Songs.IndexOf(CurrentSong);
        return index >= 0 && index < SelectedPlaylist.Songs.Count - 1;
    }

    private void PlayNext()
    {
        if (SelectedPlaylist is null || CurrentSong is null)
        {
            return;
        }

        var index = SelectedPlaylist.Songs.IndexOf(CurrentSong);
        if (index >= 0 && index < SelectedPlaylist.Songs.Count - 1)
        {
            PlaySong(SelectedPlaylist.Songs[index + 1]);
        }
    }

    private string LoadBaseDirectory()
    {
        if (File.Exists(_configPath))
        {
            var stored = File.ReadAllText(_configPath).Trim();
            if (!string.IsNullOrWhiteSpace(stored))
            {
                return stored;
            }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Musicbox", "Playlists");
    }

    private void SaveBaseDirectory()
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_configPath, BaseDirectory);
    }

    private void RefreshPlaylistDashboard()
    {
        OnPropertyChanged(nameof(PlaylistCollectionSummary));
        OnPropertyChanged(nameof(SelectedPlaylistDisplayName));
        OnPropertyChanged(nameof(SelectedPlaylistStatText));
        OnPropertyChanged(nameof(SelectedPlaylistRuntimeText));
        OnPropertyChanged(nameof(SelectedPlaylistDirectoryText));
        OnPropertyChanged(nameof(SelectedPlaylistCoverBitmap));
        OnPropertyChanged(nameof(SelectedPlaylistInitial));
        OnPropertyChanged(nameof(HasSelectedPlaylist));
        OnPropertyChanged(nameof(SelectedPlaylistEditorTitle));
        OnPropertyChanged(nameof(SelectedPlaylistEditorHint));
        OnPropertyChanged(nameof(PendingCoverText));
        OnPropertyChanged(nameof(SearchResultText));
    }

    private void RefreshFilteredPlaylists()
    {
        FilteredPlaylists.Clear();

        IEnumerable<PlaylistInfo> query = Playlists.OrderByDescending(item => item.SongCount)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase);

        var keyword = SearchKeyword.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item =>
                item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.FolderPath.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var playlist in query)
        {
            FilteredPlaylists.Add(playlist);
        }
    }

    private void LoadLyricsForSong(PlaylistSongInfo? song)
    {
        CurrentLyrics.Clear();

        if (song is null)
        {
            LyricsSourceText = "播放歌曲后将在这里显示歌词或音乐信息";
            CurrentLyrics.Add("还没有开始播放");
            CurrentLyrics.Add("从左侧歌曲列表中选择你想听的歌");
            CurrentLyrics.Add("支持自动读取同名 .lrc 歌词文件");
            CurrentLyrics.Add("没有歌词时，这里会展示歌曲信息卡片");
            return;
        }

        var lyricPath = Path.ChangeExtension(song.FilePath, ".lrc");
        if (File.Exists(lyricPath))
        {
            try
            {
                var lyricLines = File.ReadLines(lyricPath)
                    .Select(CleanLyricLine)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Distinct()
                    .Take(12)
                    .ToArray();

                if (lyricLines.Length > 0)
                {
                    LyricsSourceText = "已读取同名 LRC 歌词";
                    foreach (var line in lyricLines)
                    {
                        CurrentLyrics.Add(line);
                    }

                    return;
                }
            }
            catch
            {
                // 歌词读取失败时退回到音乐信息卡片。
            }
        }

        LyricsSourceText = "未找到歌词，已切换为音乐信息卡片";
        CurrentLyrics.Add(song.DisplayName);
        CurrentLyrics.Add(string.IsNullOrWhiteSpace(song.ArtistAlbumText) ? "未知艺人 / 专辑信息" : song.ArtistAlbumText);
        CurrentLyrics.Add($"时长 {song.DurationText} · 大小 {song.FileSizeText}");
        CurrentLyrics.Add($"文件路径: {song.FilePath}");
    }

    private static string CleanLyricLine(string line)
    {
        var cleaned = LyricTimestampRegex.Replace(line, string.Empty).Trim();
        return cleaned;
    }

    private static string FormatTime(TimeSpan value)
    {
        if (value.TotalHours >= 1)
        {
            return value.ToString(@"h\:mm\:ss");
        }

        return value.ToString(@"m\:ss");
    }

    private void NotifyCommandState()
    {
        CreatePlaylistCommand?.NotifyCanExecuteChanged();
        ClearCreateCoverCommand?.NotifyCanExecuteChanged();
        RenamePlaylistCommand?.NotifyCanExecuteChanged();
        DeletePlaylistCommand?.NotifyCanExecuteChanged();
        BrowseCoverCommand?.NotifyCanExecuteChanged();
        ApplyCoverCommand?.NotifyCanExecuteChanged();
        RemoveCoverCommand?.NotifyCanExecuteChanged();
        AddSongsCommand?.NotifyCanExecuteChanged();
        DeleteSelectedSongCommand?.NotifyCanExecuteChanged();
        OpenFolderCommand?.NotifyCanExecuteChanged();
        TogglePlaybackCommand?.NotifyCanExecuteChanged();
        PreviousSongCommand?.NotifyCanExecuteChanged();
        NextSongCommand?.NotifyCanExecuteChanged();
    }

    private static bool TryApplyCoverToPlaylist(PlaylistInfo playlist, string? sourceCoverPath, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(sourceCoverPath) || !File.Exists(sourceCoverPath))
        {
            playlist.CoverImagePath = string.Empty;
            return true;
        }

        try
        {
            PlaylistInfo.DeleteExistingCoverFiles(playlist.FolderPath);
            var extension = Path.GetExtension(sourceCoverPath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            var destination = Path.Combine(playlist.FolderPath, $"{PlaylistInfo.CoverFileBaseName}{extension}");
            File.Copy(sourceCoverPath, destination, true);
            playlist.CoverImagePath = destination;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            errorMessage = $"没有权限写入歌单封面: {playlist.FolderPath}";
            return false;
        }
        catch (IOException)
        {
            errorMessage = $"歌单封面文件正在被占用: {playlist.FolderPath}";
            return false;
        }
    }
}
