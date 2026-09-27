using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Musicbox.Models;
using Musicbox.ViewModels.Pages;

namespace Musicbox.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private object currentPage;

    [ObservableProperty]
    private AppSection currentSection = AppSection.Home;

    [ObservableProperty]
    private string pageTitle = "首页";

    public HomePageViewModel HomePage { get; }

    public ConvertPageViewModel ConvertPage { get; }

    public DownloadPageViewModel DownloadPage { get; }

    public PlaylistPageViewModel PlaylistPage { get; }

    public IRelayCommand ShowHomeCommand { get; }

    public IRelayCommand ShowConvertCommand { get; }

    public IRelayCommand ShowDownloadCommand { get; }

    public IRelayCommand ShowPlaylistCommand { get; }

    public MainWindowViewModel(
        HomePageViewModel homePage,
        ConvertPageViewModel convertPage,
        DownloadPageViewModel downloadPage,
        PlaylistPageViewModel playlistPage)
    {
        HomePage = homePage;
        ConvertPage = convertPage;
        DownloadPage = downloadPage;
        PlaylistPage = playlistPage;
        CurrentPage = HomePage;

        ShowHomeCommand = new RelayCommand(() => Navigate(AppSection.Home));
        ShowConvertCommand = new RelayCommand(() => Navigate(AppSection.Convert));
        ShowDownloadCommand = new RelayCommand(() => Navigate(AppSection.Download));
        ShowPlaylistCommand = new RelayCommand(() => Navigate(AppSection.Playlists));
    }

    public bool IsHomeSelected => CurrentSection == AppSection.Home;

    public bool IsConvertSelected => CurrentSection == AppSection.Convert;

    public bool IsDownloadSelected => CurrentSection == AppSection.Download;

    public bool IsPlaylistSelected => CurrentSection == AppSection.Playlists;

    partial void OnCurrentSectionChanged(AppSection value)
    {
        OnPropertyChanged(nameof(IsHomeSelected));
        OnPropertyChanged(nameof(IsConvertSelected));
        OnPropertyChanged(nameof(IsDownloadSelected));
        OnPropertyChanged(nameof(IsPlaylistSelected));
    }

    private void Navigate(AppSection section)
    {
        CurrentSection = section;
        switch (section)
        {
            case AppSection.Home:
                CurrentPage = HomePage;
                PageTitle = "首页";
                break;
            case AppSection.Convert:
                CurrentPage = ConvertPage;
                PageTitle = "NCM 转换";
                break;
            case AppSection.Download:
                CurrentPage = DownloadPage;
                PageTitle = "歌曲下载";
                break;
            case AppSection.Playlists:
                CurrentPage = PlaylistPage;
                PageTitle = "歌单中心";
                break;
        }
    }
}
