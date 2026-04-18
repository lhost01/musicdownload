using System;
using System.Windows.Media;
using System.Windows.Threading;

namespace 网易云音乐下载.Services
{
    /// <summary>
    /// 音乐播放器服务
    /// </summary>
    public class MusicPlayerService
    {
        private MediaPlayer _mediaPlayer;
        private DispatcherTimer _positionTimer;

        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public TimeSpan CurrentPosition { get { return _mediaPlayer?.Position ?? TimeSpan.Zero; } }
        public TimeSpan TotalDuration { get; private set; }
        public double Volume { get { return _mediaPlayer?.Volume ?? 0.5; } set { if (_mediaPlayer != null) _mediaPlayer.Volume = value; } }

        public event EventHandler PlaybackStarted;
        public event EventHandler PlaybackPaused;
        public event EventHandler PlaybackStopped;
        public event EventHandler PlaybackCompleted;
        public event EventHandler<TimeSpan> PositionChanged;
        public event EventHandler<TimeSpan> DurationChanged;

        public MusicPlayerService()
        {
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.MediaEnded += OnMediaEnded;
            _mediaPlayer.MediaOpened += OnMediaOpened;

            _positionTimer = new DispatcherTimer();
            _positionTimer.Interval = TimeSpan.FromMilliseconds(200);
            _positionTimer.Tick += OnPositionTimerTick;
        }

        /// <summary>
        /// 播放指定文件
        /// </summary>
        public void Play(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return;

                _mediaPlayer.Open(new Uri(filePath));
                _mediaPlayer.Play();
                IsPlaying = true;
                IsPaused = false;
                _positionTimer.Start();
                PlaybackStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("播放失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        public void Pause()
        {
            if (IsPlaying && !IsPaused)
            {
                _mediaPlayer.Pause();
                IsPaused = true;
                _positionTimer.Stop();
                PlaybackPaused?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 继续播放
        /// </summary>
        public void Resume()
        {
            if (IsPlaying && IsPaused)
            {
                _mediaPlayer.Play();
                IsPaused = false;
                _positionTimer.Start();
                PlaybackStarted?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        public void Stop()
        {
            _mediaPlayer.Stop();
            IsPlaying = false;
            IsPaused = false;
            _positionTimer.Stop();
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 设置播放位置
        /// </summary>
        public void SetPosition(TimeSpan position)
        {
            if (_mediaPlayer != null && position <= TotalDuration)
            {
                _mediaPlayer.Position = position;
            }
        }

        /// <summary>
        /// 设置音量 (0.0 - 1.0)
        /// </summary>
        public void SetVolume(double volume)
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Volume = Math.Max(0, Math.Min(1, volume));
            }
        }

        private void OnMediaOpened(object sender, EventArgs e)
        {
            if (_mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                TotalDuration = _mediaPlayer.NaturalDuration.TimeSpan;
                DurationChanged?.Invoke(this, TotalDuration);
            }
        }

        private void OnMediaEnded(object sender, EventArgs e)
        {
            IsPlaying = false;
            IsPaused = false;
            _positionTimer.Stop();
            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void OnPositionTimerTick(object sender, EventArgs e)
        {
            PositionChanged?.Invoke(this, _mediaPlayer.Position);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _positionTimer?.Stop();
            _mediaPlayer?.Stop();
            _mediaPlayer?.Close();
        }
    }
}
