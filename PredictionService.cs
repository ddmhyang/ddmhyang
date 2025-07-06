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
using System.Windows; // [오류 수정] MessageBox를 사용하기 위해 추가


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
            try // <--- 여기부터 위기 상황에 대비 시작!
            {
                if (!File.Exists(_timeLogFilePath))
                {
                    // 파일이 없으면 훈련을 시도조차 하지 않고 조용히 종료합니다.
                    return;
                }

                var json = File.ReadAllText(_timeLogFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    // 파일 내용은 있지만 비어있을 경우에도 종료합니다.
                    return;
                }

                var allLogs = JsonSerializer.Deserialize<List<TimeLogEntry>>(json);

                var trainingData = allLogs
                    .Where(log => log.FocusScore > 0)
                    .ToList();

                // 학습할 데이터가 10개 미만이면 훈련이 의미가 없으므로 종료합니다.
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
                // 만약 위 과정 중 어디선가 예상치 못한 오류가 발생하면,
                // 프로그램을 끄지 않고, 어떤 오류인지 메시지를 보여줍니다. (디버깅에 유용)
                MessageBox.Show($"AI 모델 훈련 중 오류가 발생했습니다: {ex.Message}");
            }
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
