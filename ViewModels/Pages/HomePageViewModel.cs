using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Musicbox.Services;

namespace Musicbox.ViewModels.Pages;

public partial class HomePageViewModel : ViewModelBase
{
    private readonly DispatcherTimer _timer;
    private readonly QuoteService _quoteService;

    [ObservableProperty]
    private string currentTimeText = string.Empty;

    [ObservableProperty]
    private string currentDateText = string.Empty;

    [ObservableProperty]
    private string dailyQuote = string.Empty;

    public HomePageViewModel(QuoteService quoteService)
    {
        _quoteService = quoteService;
        RefreshClock();
        DailyQuote = _quoteService.GetDailyQuote();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) => RefreshClock();
        _timer.Start();
    }

    private void RefreshClock()
    {
        var now = DateTime.Now;
        CurrentTimeText = now.ToString("HH:mm:ss");
        CurrentDateText = now.ToString("yyyy年MM月dd日 dddd");
    }
}
