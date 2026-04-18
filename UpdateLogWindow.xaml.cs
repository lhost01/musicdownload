using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace 网易云音乐下载
{
    /// <summary>
    /// 更新日志窗口
    /// </summary>
    public partial class UpdateLogWindow : Window
    {
        private const string CurrentUpdateLogVersion = "2026-04-19";
        private static readonly string UpdateLogStatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "网易云音乐下载",
            "update-log-state.json");

        public UpdateLogWindow()
        {
            InitializeComponent();
            LoadPreference();
        }

        public static bool ShouldShowForCurrentVersion()
        {
            try
            {
                var state = LoadState();
                return !string.Equals(state?.SkippedVersion, CurrentUpdateLogVersion, StringComparison.Ordinal);
            }
            catch
            {
                return true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SavePreferenceAndClose();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            SavePreferenceAndClose();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void SavePreferenceAndClose()
        {
            SavePreference();
            Close();
        }

        private void SavePreference()
        {
            try
            {
                string directory = Path.GetDirectoryName(UpdateLogStatePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var state = new UpdateLogState
                {
                    SkippedVersion = SkipThisVersionCheckBox.IsChecked == true ? CurrentUpdateLogVersion : string.Empty
                };

                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(UpdateLogStatePath, json);
            }
            catch
            {
            }
        }

        private void LoadPreference()
        {
            try
            {
                var state = LoadState();
                SkipThisVersionCheckBox.IsChecked = string.Equals(
                    state?.SkippedVersion,
                    CurrentUpdateLogVersion,
                    StringComparison.Ordinal);
            }
            catch
            {
                SkipThisVersionCheckBox.IsChecked = false;
            }
        }

        private static UpdateLogState LoadState()
        {
            if (!File.Exists(UpdateLogStatePath))
            {
                return new UpdateLogState();
            }

            string json = File.ReadAllText(UpdateLogStatePath);
            return JsonSerializer.Deserialize<UpdateLogState>(json) ?? new UpdateLogState();
        }

        private sealed class UpdateLogState
        {
            public string SkippedVersion { get; set; } = string.Empty;
        }
    }
}
