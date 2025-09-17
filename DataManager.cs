// 파일: DataManager.cs (최종 수정본)

using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace WorkPartner
{
    public static class DataManager
    {
        public static event Action SettingsUpdated;

        private static readonly string AppDataFolder;

        public static string SettingsFilePath { get; }
        public static string TimeLogFilePath { get; }
        public static string TasksFilePath { get; }
        public static string TodosFilePath { get; }
        public static string MemosFilePath { get; }
        public static string ModelFilePath { get; }
        public static string ItemsDbFilePath { get; }

        static DataManager()
        {
            AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkPartner");
            Directory.CreateDirectory(AppDataFolder);
            SettingsFilePath = Path.Combine(AppDataFolder, "app_settings.json");
            TimeLogFilePath = Path.Combine(AppDataFolder, "timelogs.json");
            TasksFilePath = Path.Combine(AppDataFolder, "tasks.json");
            TodosFilePath = Path.Combine(AppDataFolder, "todos.json");
            MemosFilePath = Path.Combine(AppDataFolder, "memos.json");
            ModelFilePath = Path.Combine(AppDataFolder, "FocusPredictionModel.zip");
            ItemsDbFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "items_db.json");
        }

        public static AppSettings LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            return new AppSettings();
        }

        public static void SaveSettings(AppSettings settings)
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(SettingsFilePath, json);
        }

        public static void SaveSettingsAndNotify(AppSettings settings)
        {
            SaveSettings(settings);
            SettingsUpdated?.Invoke();
        }

        public static void PrepareFileForEditing(string sourceFileName)
        {
            try
            {
                string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, sourceFileName);
                string destinationPath = Path.Combine(AppDataFolder, sourceFileName);

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

        /// <summary>
        /// 과목(작업) 이름에 해당하는 색상 코드를 반환합니다.
        /// 설정된 색상이 없으면 기본 색상을 반환합니다.
        /// </summary>
        /// <param name="taskName">색상을 찾을 과목 이름</param>
        /// <returns>16진수 색상 코드 (예: "#FF0000")</returns>
        public static string GetColorForTask(string taskName)
        {
            var settings = LoadSettings();
            if (settings.TaskColors != null && settings.TaskColors.TryGetValue(taskName, out var color))
            {
                return color;
            }
            return "#808080"; // 설정된 색상이 없을 때 사용할 기본 색상 (회색)
        }
    }
}