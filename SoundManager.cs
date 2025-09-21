using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;

namespace WorkPartner
{
    /// <summary>
    /// 여러 오디오 파일을 동시에 재생하고 볼륨을 조절하는 클래스입니다.
    /// </summary>
    public class SoundManager : IDisposable
    {
        private readonly Dictionary<string, MediaPlayer> _players = new Dictionary<string, MediaPlayer>();

        public SoundManager()
        {
            // 백색 소음 파일들을 초기화합니다.
            // 중요: Sounds 폴더를 만들고, 여기에 wave.mp3, forest.mp3, rain.mp3, campfire.mp3 파일을 추가한 후
            // Visual Studio에서 각 파일의 속성(Properties) -> Build Action을 'Resource'로 설정해야 합니다.
            InitializePlayer("Wave", "Sounds/wave.mp3");
            InitializePlayer("Forest", "Sounds/forest.mp3");
            InitializePlayer("Rain", "Sounds/rain.mp3");
            InitializePlayer("Campfire", "Sounds/campfire.mp3");
        }

        private void InitializePlayer(string name, string relativePath)
        {
            try
            {
                var player = new MediaPlayer();
                // 미디어가 끝나면 다시 재생 (루프)
                player.MediaEnded += (s, e) => {
                    player.Position = TimeSpan.Zero;
                    player.Play();
                };

                // pack URI를 사용하여 리소스 파일에 접근합니다.
                var uri = new Uri($"pack://application:,,,/WorkPartner;component/{relativePath}", UriKind.Absolute);
                player.Open(uri);

                _players[name] = player;
            }
            catch (Exception ex)
            {
                // 파일이 없거나 로드에 실패할 경우 콘솔에 오류를 출력합니다.
                System.Diagnostics.Debug.WriteLine($"Error initializing player for {name}: {ex.Message}");
            }
        }

        /// <summary>
        /// 모든 사운드를 재생 시작합니다. (볼륨이 0이면 소리가 나지 않습니다)
        /// </summary>
        public void PlayAll()
        {
            foreach (var player in _players.Values)
            {
                player.Play();
            }
        }

        /// <summary>
        /// 모든 사운드를 중지합니다.
        /// </summary>
        public void StopAll()
        {
            foreach (var player in _players.Values)
            {
                player.Stop();
            }
        }

        /// <summary>
        /// 특정 사운드의 볼륨을 조절합니다.
        /// </summary>
        /// <param name="soundName">"Wave", "Forest", "Rain", "Campfire"</param>
        /// <param name="volume">0.0 (음소거) ~ 1.0 (최대 볼륨)</param>
        public void SetVolume(string soundName, double volume)
        {
            if (_players.TryGetValue(soundName, out MediaPlayer player))
            {
                player.Volume = volume;
            }
        }

        /// <summary>
        /// 저장된 볼륨 설정으로 모든 사운드의 볼륨을 한 번에 설정합니다.
        /// </summary>
        public void SetAllVolumes(Dictionary<string, double> volumes)
        {
            if (volumes == null) return;
            foreach (var kvp in volumes)
            {
                SetVolume(kvp.Key, kvp.Value);
            }
        }

        // 리소스 정리를 위한 Dispose 메서드
        public void Dispose()
        {
            StopAll();
            foreach (var player in _players.Values)
            {
                player.Close();
            }
            _players.Clear();
        }
    }
}

