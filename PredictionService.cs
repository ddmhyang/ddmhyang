// 파일: PredictionService.cs (수정)
// [수정] MessageBox를 사용하기 위해 'System.Windows' 네임스페이스를 추가했습니다.
// [수정] 학습 데이터를 ModelInput 형태로 변환하는 과정을 추가합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Trainers.FastTree;
using System.Windows;

namespace WorkPartner.AI
{
    public class PredictionService
    {
        private readonly string _timeLogFilePath = "timelogs.json";
        private readonly string _modelPath = "FocusPredictionModel.zip";
        private MLContext _mlContext;
        private ITransformer _model;

        public PredictionService()
        {
            _mlContext = new MLContext(seed: 0);
        }

        public void TrainModel()
        {
            try
            {
                if (!File.Exists(_timeLogFilePath)) return;

                var json = File.ReadAllText(_timeLogFilePath);
                if (string.IsNullOrWhiteSpace(json)) return;

                var allLogs = JsonSerializer.Deserialize<List<TimeLogEntry>>(json);

                // [수정] TimeLogEntry 리스트를 ModelInput 리스트로 변환합니다.
                var modelInputData = allLogs
                    .Where(log => log.FocusScore > 0)
                    .Select(log => new ModelInput
                    {
                        DayOfWeek = (float)log.StartTime.DayOfWeek,
                        Hour = (float)log.StartTime.Hour,
                        Duration = (float)log.Duration.TotalMinutes,
                        TaskName = log.TaskText,
                        FocusScore = log.FocusScore
                    }).ToList();

                if (modelInputData.Count < 10) return;

                // [수정] 변환된 modelInputData를 사용하여 DataView를 생성합니다.
                var dataView = _mlContext.Data.LoadFromEnumerable(modelInputData);

                // [수정] 파이프라인에서 더 이상 FocusScore를 복사할 필요가 없습니다.
                var pipeline = _mlContext.Transforms.Categorical.OneHotEncoding(outputColumnName: "TaskNameEncoded", inputColumnName: "TaskName")
                    .Append(_mlContext.Transforms.Concatenate("Features", "DayOfWeek", "Hour", "Duration", "TaskNameEncoded"))
                    .Append(_mlContext.Regression.Trainers.FastTree());

                _model = pipeline.Fit(dataView);
                _mlContext.Model.Save(_model, dataView.Schema, _modelPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"AI 모델 훈련 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        public float Predict(ModelInput input)
        {
            if (_model == null)
            {
                if (File.Exists(_modelPath))
                {
                    _model = _mlContext.Model.Load(_modelPath, out _);
                }
                else
                {
                    TrainModel();
                    if (File.Exists(_modelPath))
                    {
                        _model = _mlContext.Model.Load(_modelPath, out _);
                    }
                    else
                    {
                        return 0;
                    }
                }
            }

            var predictionEngine = _mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(_model);
            var result = predictionEngine.Predict(input);
            return result.PredictedFocusScore;
        }
    }
}
