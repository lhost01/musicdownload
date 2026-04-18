using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace 网易云音乐下载
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (UpdateLogWindow.ShouldShowForCurrentVersion())
            {
                // 显示更新日志窗口（非模态，与主窗口同时显示）
                var updateLogWindow = new UpdateLogWindow();
                updateLogWindow.Show();
            }
        }
    }
}
