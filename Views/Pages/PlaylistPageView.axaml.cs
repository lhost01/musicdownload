using Avalonia.Controls;
using Avalonia.Input;
using Musicbox.Models;
using Musicbox.ViewModels.Pages;

namespace Musicbox.Views.Pages;

public partial class PlaylistPageView : UserControl
{
    public PlaylistPageView()
    {
        InitializeComponent();
    }

    private void OnSongCardDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: PlaylistSongInfo song })
        {
            return;
        }

        if (DataContext is PlaylistPageViewModel viewModel)
        {
            viewModel.SelectedSong = song;
            viewModel.PlaySongCommand.Execute(song);
        }
    }
}
