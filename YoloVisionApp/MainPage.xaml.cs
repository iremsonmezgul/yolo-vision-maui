using System.Diagnostics;
using YoloVisionApp.Models;
using YoloVisionApp.Services;

namespace YoloVisionApp
{
    public partial class MainPage : ContentPage
    {
        readonly ICameraFrameSource cameraFrameSource;
        readonly IObjectDetector detector;
        readonly Stopwatch fpsStopwatch = new();

        CameraFrame? latestFrame;
        CameraFrame? latestDetectionFrame;
        int drawPending;
        int detectionPending;
        int framesSinceLastFpsUpdate;
        double lastInferenceMs;

        public MainPage(ICameraFrameSource cameraFrameSource, IObjectDetector detector)
        {
            InitializeComponent();
            this.cameraFrameSource = cameraFrameSource;
            this.detector = detector;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await InitializeDetectorAsync();
            await StartCameraAsync();
        }

        async Task InitializeDetectorAsync()
        {
            try
            {
                await detector.InitializeAsync();
            }
            catch (Exception ex)
            {
                ShowError($"Model yüklenemedi: {ex.Message}");
            }
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            await StopCameraAsync();
        }

        async void OnToggleClicked(object? sender, EventArgs e)
        {
            if (cameraFrameSource.IsRunning)
                await StopCameraAsync();
            else
                await StartCameraAsync();
        }

        async Task StartCameraAsync()
        {
            if (cameraFrameSource.IsRunning)
                return;

            if (detector.IsReady)
                ShowError(null);

            var status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                ShowError("Kamera izni verilmedi.");
                return;
            }

            try
            {
                cameraFrameSource.FrameArrived += OnFrameArrived;
                fpsStopwatch.Restart();
                framesSinceLastFpsUpdate = 0;
                await cameraFrameSource.StartAsync();
                ToggleButton.Text = "Durdur";
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or InvalidOperationException)
            {
                cameraFrameSource.FrameArrived -= OnFrameArrived;
                ShowError($"Kamera başlatılamadı: {ex.Message}");
            }
        }

        async Task StopCameraAsync()
        {
            cameraFrameSource.FrameArrived -= OnFrameArrived;
            await cameraFrameSource.StopAsync();
            Interlocked.Exchange(ref latestDetectionFrame, null);
            FrameView.Detections = null;
            ToggleButton.Text = "Başlat";
        }

        void OnFrameArrived(object? sender, CameraFrame frame)
        {
            Interlocked.Exchange(ref latestFrame, frame);

            if (detector.IsReady)
            {
                Interlocked.Exchange(ref latestDetectionFrame, frame);
                if (Interlocked.Exchange(ref detectionPending, 1) == 0)
                    Task.Run(RunDetectionLoop);
            }

            if (Interlocked.Exchange(ref drawPending, 1) == 1)
                return;

            MainThread.BeginInvokeOnMainThread(DrawLatestFrame);
        }

        void RunDetectionLoop()
        {
            while (true)
            {
                var frame = Interlocked.Exchange(ref latestDetectionFrame, null);
                if (frame is not null)
                    DetectAndShow(frame);

                Interlocked.Exchange(ref detectionPending, 0);

                // A frame may have arrived after the exchange above but before the flag was cleared;
                // its sender saw detectionPending == 1 and did not start a worker, so keep going.
                if (Volatile.Read(ref latestDetectionFrame) is null || Interlocked.Exchange(ref detectionPending, 1) == 1)
                    return;
            }
        }

        void DetectAndShow(CameraFrame frame)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var detections = detector.Detect(frame);
                var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    lastInferenceMs = elapsedMs;
                    if (cameraFrameSource.IsRunning)
                        FrameView.Detections = detections;
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() => ShowError($"Tespit hatası: {ex.Message}"));
            }
        }

        void DrawLatestFrame()
        {
            Interlocked.Exchange(ref drawPending, 0);

            var frame = Interlocked.Exchange(ref latestFrame, null);
            if (frame is null)
                return;

            FrameView.CurrentFrame = frame;
            UpdateStats(frame);
        }

        void UpdateStats(CameraFrame frame)
        {
            framesSinceLastFpsUpdate++;
            var elapsed = fpsStopwatch.Elapsed.TotalSeconds;
            if (elapsed < 1)
                return;

            var yolo = detector.IsReady ? $" · YOLO {lastInferenceMs:F0} ms" : "";
            StatsLabel.Text = $"{framesSinceLastFpsUpdate / elapsed:F1} FPS · {frame.Width}x{frame.Height}{yolo}";
            framesSinceLastFpsUpdate = 0;
            fpsStopwatch.Restart();
        }

        void ShowError(string? message)
        {
            ErrorLabel.Text = message;
            ErrorLabel.IsVisible = message is not null;
        }
    }
}
