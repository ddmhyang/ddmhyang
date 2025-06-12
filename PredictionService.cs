// 파일 이름: PredictionService.cs
// 역할: ML.NET 모델을 훈련시키고, 예측을 수행하는 모든 AI 관련 작업을 담당합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.ML;
// [수정] FastTree 알고리즘을 사용하기 위해 네임스페이스를 추가합니다.
using Microsoft.ML.Trainers.FastTree;

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

        // 1. 모델 훈련
        public void TrainModel()
        {
            if (!File.Exists(_timeLogFilePath)) return;

            var json = File.ReadAllText(_timeLogFilePath);
            var allLogs = JsonSerializer.Deserialize<List<TimeLogEntry>>(json);

            var trainingData = allLogs
                .Where(log => log.FocusScore > 0)
                .Select(log => new ModelInput
                {
                    DayOfWeek = (float)log.StartTime.DayOfWeek,
                    Hour = (float)log.StartTime.Hour,
                    Duration = (float)log.Duration.TotalMinutes,
                    TaskName = log.TaskText,
                    FocusScore = log.FocusScore
                }).ToList();

            if (trainingData.Count < 10) return;

            var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

            // 2. 파이프라인 구축
            var pipeline = _mlContext.Transforms.CopyColumns(outputColumnName: "Label", inputColumnName: "FocusScore")
                .Append(_mlContext.Transforms.Categorical.OneHotEncoding(outputColumnName: "TaskNameEncoded", inputColumnName: "TaskName"))
                .Append(_mlContext.Transforms.Concatenate("Features", "DayOfWeek", "Hour", "Duration", "TaskNameEncoded"))
                .Append(_mlContext.Regression.Trainers.FastTree()); // 이제 이 부분이 정상적으로 작동합니다.

            // 3. 모델 훈련
            _model = pipeline.Fit(dataView);

            // 4. 훈련된 모델을 파일로 저장
            _mlContext.Model.Save(_model, dataView.Schema, _modelPath);
        }

        // 5. 집중도 예측
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
