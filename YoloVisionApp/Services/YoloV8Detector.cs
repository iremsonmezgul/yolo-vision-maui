using Microsoft.ML.OnnxRuntime;
using System.Text.RegularExpressions;
using YoloVision.Core;
using YoloVisionApp.Models;

namespace YoloVisionApp.Services
{
    public sealed partial class YoloV8Detector : IObjectDetector, IDisposable
    {
        const string ModelFileName = "yolov8n.onnx";
        const float ConfidenceThreshold = 0.25f;
        const float IouThreshold = 0.45f;
        const int MaxDetections = 100;
        const float PadValue = 114f / 255f;

        readonly SemaphoreSlim initLock = new(1, 1);

        InferenceSession? session;
        RunOptions? runOptions;
        OrtValue? inputValue;
        float[] input = [];
        string[] inputNames = [];
        string[] outputNames = [];
        string[] labels = [];
        int inputSize;

        public bool IsReady => session is not null;

        public async Task InitializeAsync()
        {
            if (IsReady)
                return;

            await initLock.WaitAsync();
            try
            {
                if (IsReady)
                    return;

                using var stream = await FileSystem.OpenAppPackageFileAsync(ModelFileName);
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                var modelBytes = memory.ToArray();

                await Task.Run(() => Load(modelBytes));
            }
            finally
            {
                initLock.Release();
            }
        }

        void Load(byte[] modelBytes)
        {
            var newSession = new InferenceSession(modelBytes);

            var (inputName, inputMeta) = newSession.InputMetadata.First();
            inputSize = inputMeta.Dimensions[^1];
            inputNames = [inputName];
            outputNames = [newSession.OutputMetadata.Keys.First()];
            labels = ParseLabels(newSession.ModelMetadata.CustomMetadataMap);

            input = new float[3 * inputSize * inputSize];
            inputValue = OrtValue.CreateTensorValueFromMemory(input, [1, 3, inputSize, inputSize]);
            runOptions = new RunOptions();
            session = newSession;
        }

        static string[] ParseLabels(Dictionary<string, string> metadata)
        {
            if (!metadata.TryGetValue("names", out var names))
                return [];

            var matches = LabelRegex().Matches(names);
            var result = new string[matches.Count];
            foreach (Match match in matches)
            {
                var id = int.Parse(match.Groups[1].Value);
                if (id < result.Length)
                    result[id] = match.Groups[2].Value;
            }
            return result;
        }

        [GeneratedRegex(@"(\d+):\s*'([^']*)'")]
        private static partial Regex LabelRegex();

        public IReadOnlyList<Detection> Detect(CameraFrame frame)
        {
            if (session is null || inputValue is null)
                throw new InvalidOperationException("Call InitializeAsync before Detect.");

            var letterbox = Preprocess(frame);

            using var outputs = session.Run(runOptions, inputNames, [inputValue], outputNames);
            var output = outputs[0];
            var shape = output.GetTensorTypeAndShape().Shape;
            return Postprocess(output.GetTensorDataAsSpan<float>(), (int)shape[1], (int)shape[2], letterbox);
        }

        readonly record struct Letterbox(float Scale, float PadX, float PadY, int UprightWidth, int UprightHeight);

        Letterbox Preprocess(CameraFrame frame)
        {
            int width = frame.Width, height = frame.Height;
            var rotation = ((frame.RotationDegrees % 360) + 360) % 360;
            var sideways = rotation is 90 or 270;
            var uprightWidth = sideways ? height : width;
            var uprightHeight = sideways ? width : height;

            var scale = Math.Min(inputSize / (float)uprightWidth, inputSize / (float)uprightHeight);
            var padX = (inputSize - uprightWidth * scale) / 2;
            var padY = (inputSize - uprightHeight * scale) / 2;

            var (redOffset, blueOffset) = frame.Format == CameraPixelFormat.Bgra8888 ? (2, 0) : (0, 2);
            var pixels = frame.Pixels;
            var plane = inputSize * inputSize;

            for (var dy = 0; dy < inputSize; dy++)
            {
                var v = (int)((dy + 0.5f - padY) / scale);
                for (var dx = 0; dx < inputSize; dx++)
                {
                    var index = dy * inputSize + dx;
                    var u = (int)((dx + 0.5f - padX) / scale);

                    if (dx + 0.5f < padX || dy + 0.5f < padY || u >= uprightWidth || v >= uprightHeight)
                    {
                        input[index] = input[plane + index] = input[2 * plane + index] = PadValue;
                        continue;
                    }

                    var (x, y) = rotation switch
                    {
                        90 => (v, height - 1 - u),
                        180 => (width - 1 - u, height - 1 - v),
                        270 => (width - 1 - v, u),
                        _ => (u, v),
                    };

                    var p = (y * width + x) * 4;
                    input[index] = pixels[p + redOffset] / 255f;
                    input[plane + index] = pixels[p + 1] / 255f;
                    input[2 * plane + index] = pixels[p + blueOffset] / 255f;
                }
            }

            return new Letterbox(scale, padX, padY, uprightWidth, uprightHeight);
        }

        IReadOnlyList<Detection> Postprocess(ReadOnlySpan<float> output, int rows, int anchors, Letterbox letterbox)
        {
            var classCount = rows - 4;
            var candidates = new List<Detection>();

            for (var i = 0; i < anchors; i++)
            {
                var bestClass = 0;
                var bestScore = 0f;
                for (var c = 0; c < classCount; c++)
                {
                    var score = output[(4 + c) * anchors + i];
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestClass = c;
                    }
                }

                if (bestScore < ConfidenceThreshold)
                    continue;

                var cx = (output[i] - letterbox.PadX) / letterbox.Scale;
                var cy = (output[anchors + i] - letterbox.PadY) / letterbox.Scale;
                var w = output[2 * anchors + i] / letterbox.Scale;
                var h = output[3 * anchors + i] / letterbox.Scale;

                var left = Math.Clamp(cx - w / 2, 0, letterbox.UprightWidth);
                var top = Math.Clamp(cy - h / 2, 0, letterbox.UprightHeight);
                var right = Math.Clamp(cx + w / 2, 0, letterbox.UprightWidth);
                var bottom = Math.Clamp(cy + h / 2, 0, letterbox.UprightHeight);
                if (right <= left || bottom <= top)
                    continue;

                var label = bestClass < labels.Length && labels[bestClass] is { } name ? name : $"class {bestClass}";
                candidates.Add(new Detection(bestClass, label, bestScore, left, top, right - left, bottom - top));
            }

            return DetectionMath.NonMaxSuppression(candidates, IouThreshold, MaxDetections);
        }

        public void Dispose()
        {
            inputValue?.Dispose();
            runOptions?.Dispose();
            session?.Dispose();
            initLock.Dispose();
        }
    }
}
