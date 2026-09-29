using Android.Runtime;
using AndroidX.Camera.Core;
using AndroidX.Camera.Core.ResolutionSelector;
using AndroidX.Camera.Lifecycle;
using AndroidX.Core.Content;
using AndroidX.Lifecycle;
using Java.Util.Concurrent;
using YoloVisionApp.Models;
using Size = Android.Util.Size;

namespace YoloVisionApp.Services
{
    public sealed class AndroidCameraFrameSource : ICameraFrameSource
    {
        static readonly Size TargetResolution = new(640, 480);

        IExecutorService? analyzerExecutor;
        ProcessCameraProvider? cameraProvider;
        ImageAnalysis? imageAnalysis;

        public bool IsRunning => imageAnalysis is not null;

        public event EventHandler<CameraFrame>? FrameArrived;

        public Task StartAsync(CancellationToken cancellationToken = default) =>
            MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (imageAnalysis is not null)
                    return;

                var activity = Platform.CurrentActivity
                    ?? throw new InvalidOperationException("Etkin bir Android activity bulunamadı.");
                if (activity is not ILifecycleOwner lifecycleOwner)
                    throw new InvalidOperationException("Activity bir ILifecycleOwner değil.");

                cameraProvider ??= await GetCameraProviderAsync(activity);
                cancellationToken.ThrowIfCancellationRequested();

                var resolutionSelector = new ResolutionSelector.Builder()
                    .SetResolutionStrategy(new ResolutionStrategy(TargetResolution, ResolutionStrategy.FallbackRuleClosestHigherThenLower))
                    .Build();

                var analysis = new ImageAnalysis.Builder()
                    .SetOutputImageFormat(ImageAnalysis.OutputImageFormatRgba8888)
                    .SetBackpressureStrategy(ImageAnalysis.StrategyKeepOnlyLatest)
                    .SetResolutionSelector(resolutionSelector)
                    .Build();

                analyzerExecutor ??= Executors.NewSingleThreadExecutor()!;
                analysis.SetAnalyzer(analyzerExecutor, new FrameAnalyzer(this));

                cameraProvider.UnbindAll();
                cameraProvider.BindToLifecycle(lifecycleOwner, CameraSelector.DefaultBackCamera, analysis);
                imageAnalysis = analysis;
            });

        public Task StopAsync() =>
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                imageAnalysis?.ClearAnalyzer();
                cameraProvider?.UnbindAll();
                imageAnalysis = null;
            });

        static Task<ProcessCameraProvider> GetCameraProviderAsync(Android.Content.Context context)
        {
            var completion = new TaskCompletionSource<ProcessCameraProvider>();
            var future = ProcessCameraProvider.GetInstance(context);

            future.AddListener(new Java.Lang.Runnable(() =>
            {
                try
                {
                    completion.SetResult((ProcessCameraProvider)future.Get()!);
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }), ContextCompat.GetMainExecutor(context));

            return completion.Task;
        }

        void OnFrame(CameraFrame frame) => FrameArrived?.Invoke(this, frame);

        sealed class FrameAnalyzer(AndroidCameraFrameSource owner) : Java.Lang.Object, ImageAnalysis.IAnalyzer
        {
            public void Analyze(IImageProxy image)
            {
                try
                {
                    owner.OnFrame(ToCameraFrame(image));
                }
                finally
                {
                    // CameraX stops delivering frames until the current one is closed.
                    image.Close();
                }
            }

            static CameraFrame ToCameraFrame(IImageProxy image)
            {
                var width = image.Width;
                var height = image.Height;
                var plane = image.GetPlanes()[0];
                var rowStride = plane.RowStride;
                var packedRowBytes = width * 4;

                var pixels = new byte[packedRowBytes * height];
                var source = JNIEnv.GetDirectBufferAddress(plane.Buffer.Handle);

                for (var row = 0; row < height; row++)
                    System.Runtime.InteropServices.Marshal.Copy(source + row * rowStride, pixels, row * packedRowBytes, packedRowBytes);

                return new CameraFrame(
                    pixels,
                    width,
                    height,
                    CameraPixelFormat.Rgba8888,
                    image.ImageInfo.RotationDegrees,
                    TimeSpan.FromTicks(image.ImageInfo.Timestamp / 100));
            }
        }
    }
}
