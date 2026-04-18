using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using 网易云音乐下载.ViewModels;

namespace 网易云音乐下载
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // 设置数据上下文
            DataContext = new MainViewModel();
            
            // 订阅完成动画事件
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 窗口加载完成后的初始化
            // 可以在这里添加额外的动画或初始化逻辑
        }

        /// <summary>
        /// 标题栏鼠标左键按下 - 实现拖动窗口
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 双击最大化/还原
                if (WindowState == WindowState.Maximized)
                {
                    WindowState = WindowState.Normal;
                }
                else
                {
                    WindowState = WindowState.Maximized;
                }
            }
            else
            {
                // 拖动窗口
                DragMove();
            }
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 最小化按钮点击
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 最大化/还原按钮点击
        /// </summary>
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
        }

        /// <summary>
        /// 播放完成动画
        /// </summary>
        private void PlayCompleteAnimation(UIElement element)
        {
            if (Resources["CompleteAnimation"] is Storyboard storyboard)
            {
                Storyboard.SetTarget(storyboard, element);
                storyboard.Begin();
            }
        }

        /// <summary>
        /// 播放按钮点击动画
        /// </summary>
        private void PlayButtonClickAnimation(UIElement element)
        {
            if (Resources["ButtonClickStoryboard"] is Storyboard storyboard)
            {
                Storyboard.SetTarget(storyboard, element);
                storyboard.Begin();
            }
        }

        private MainViewModel ViewModel
        {
            get { return DataContext as MainViewModel; }
        }

        private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.BeginSeek();
        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            CompleteSeek(sender);
        }

        private void ProgressSlider_LostMouseCapture(object sender, MouseEventArgs e)
        {
            CompleteSeek(sender);
        }

        private void CompleteSeek(object sender)
        {
            if (sender is Slider slider)
            {
                ViewModel?.CompleteSeek(slider.Value);
            }
        }
    }
}
