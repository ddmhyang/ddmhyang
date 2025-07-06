// 파일: PredictionService.cs (수정)
// [수정] MessageBox를 사용하기 위해 'System.Windows' 네임스페이스를 추가했습니다.
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

                var trainingData = allLogs
                    .Where(log => log.FocusScore > 0)
                    .ToList();

                if (trainingData.Count < 10) return;

                var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

                var pipeline = _mlContext.Transforms.CopyColumns(outputColumnName: "Label", inputColumnName: "FocusScore")
                    .Append(_mlContext.Transforms.Categorical.OneHotEncoding(outputColumnName: "TaskNameEncoded", inputColumnName: "TaskName"))
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
