using System;
using System.IO;
using System.Windows; // MessageBox를 위해 추가

namespace WorkPartner
{
    public static class DataManager
    {
        public event Action SettingsUpdated;
        // 1. AppData 안에 우리 프로그램 전용 폴더 경로를 만듭니다.
        private static readonly string AppDataFolder;

        // 2. 각 파일의 전체 경로를 속성으로 만들어 쉽게 가져다 쓸 수 있게 합니다.
        public static string SettingsFilePath { get; }
        public static string TimeLogFilePath { get; }
        public static string TasksFilePath { get; }
        public static string TodosFilePath { get; }
        public static string MemosFilePath { get; }
        public static string ModelFilePath { get; }

        // 3. 읽기 전용 데이터 파일의 경로도 관리합니다.
        public static string ItemsDbFilePath { get; }

        // 프로그램이 시작될 때 단 한 번만 실행되는 생성자
        static DataManager()
        {
            // AppData 폴더 경로 설정 (예: C:\Users\사용자\AppData\Roaming\WorkPartner)
            AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkPartner");

            // 우리 앱 폴더가 없으면 새로 생성
            Directory.CreateDirectory(AppDataFolder);

            // 각 파일의 전체 경로 설정
            SettingsFilePath = Path.Combine(AppDataFolder, "app_settings.json");
            TimeLogFilePath = Path.Combine(AppDataFolder, "timelogs.json");
            TasksFilePath = Path.Combine(AppDataFolder, "tasks.json");
            TodosFilePath = Path.Combine(AppDataFolder, "todos.json");
            MemosFilePath = Path.Combine(AppDataFolder, "memos.json");
            ModelFilePath = Path.Combine(AppDataFolder, "FocusPredictionModel.zip");

            // 읽기 전용 파일은 설치 폴더 경로를 사용
            ItemsDbFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "items_db.json");
        }

        public void SaveSettingsAndNotify()
        {
            _settings.Save();
            SettingsUpdated?.Invoke();
        }

        // AI 모델 파일과 같이, 처음에는 프로그램 폴더에 있다가
        // 수정이 필요할 때 AppData로 복사해야 하는 파일을 준비하는 메서드
        public static void PrepareFileForEditing(string sourceFileName)
        {
            try
            {
                string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, sourceFileName);
                string destinationPath = Path.Combine(AppDataFolder, sourceFileName);

                // AppData에 파일이 없고, 원본 파일은 있을 때만 복사
                if (!File.Exists(destinationPath) && File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, destinationPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파일 준비 중 오류 발생: {sourceFileName}\n{ex.Message}");
            }
        }
    }
}