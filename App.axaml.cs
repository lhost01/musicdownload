using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Musicbox.Services;
using Musicbox.ViewModels;
using Musicbox.ViewModels.Pages;
using Musicbox.Views;

namespace Musicbox;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var storageService = new StorageService();
            var quoteService = new QuoteService();
            var audioConverterService = new AudioConverterService();
            var downloadService = new NeteaseDownloadService();
            var musicPlayerService = new MusicPlayerService();
            var playlistPageViewModel = new PlaylistPageViewModel(storageService, musicPlayerService);

            var mainWindowViewModel = new MainWindowViewModel(
                new HomePageViewModel(quoteService),
                new ConvertPageViewModel(storageService, audioConverterService, playlistPageViewModel),
                new DownloadPageViewModel(storageService, downloadService, playlistPageViewModel),
                playlistPageViewModel);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
