using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using YoloVisionApp.Models;

namespace YoloVisionApp.Services
{
    public sealed class WindowsCameraFrameSource : ICameraFrameSource
    {
        readonly SemaphoreSlim gate = new(1, 1);

        MediaCapture? mediaCapture;
        MediaFrameReader? frameReader;

        public bool IsRunning => frameReader is not null;

        public event EventHandler<CameraFrame>? FrameArrived;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (frameReader is not null)
                    return;

                var (group, sourceInfo) = await FindColorSourceAsync()
                    ?? throw new InvalidOperationException("Kullanılabilir bir kamera bulunamadı.");

                var capture = new MediaCapture();
                try
                {
                    await capture.InitializeAsync(new MediaCaptureInitializationSettings
                    {
                        SourceGroup = group,
                        SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                        MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                        StreamingCaptureMode = StreamingCaptureMode.Video
                    });

                    var reader = await capture.CreateFrameReaderAsync(capture.FrameSources[sourceInfo.Id], MediaEncodingSubtypes.Bgra8);
                    reader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
                    reader.FrameArrived += OnFrameArrived;

                    var status = await reader.StartAsync();
                    if (status != MediaFrameReaderStartStatus.Success)
                    {
                        reader.FrameArrived -= OnFrameArrived;
                        reader.Dispose();
                        throw new InvalidOperationException($"Kamera akışı başlatılamadı ({status}).");
                    }

                    mediaCapture = capture;
                    frameReader = reader;
                }
                catch
                {
                    capture.Dispose();
                    throw;
                }
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task StopAsync()
        {
            await gate.WaitAsync();
            try
            {
                if (frameReader is not null)
                {
                    frameReader.FrameArrived -= OnFrameArrived;
                    await frameReader.StopAsync();
                    frameReader.Dispose();
                    frameReader = null;
                }

                mediaCapture?.Dispose();
                mediaCapture = null;
            }
            finally
            {
                gate.Release();
            }
        }

        static async Task<(MediaFrameSourceGroup Group, MediaFrameSourceInfo Source)?> FindColorSourceAsync()
        {
            var groups = await MediaFrameSourceGroup.FindAllAsync();
            foreach (var group in groups)
            {
                var source = group.SourceInfos.FirstOrDefault(info =>
                    info.SourceKind == MediaFrameSourceKind.Color &&
                    (info.MediaStreamType == MediaStreamType.VideoPreview || info.MediaStreamType == MediaStreamType.VideoRecord));

                if (source is not null)
                    return (group, source);
            }

            return null;
        }

        void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            using var frameReference = sender.TryAcquireLatestFrame();
            var softwareBitmap = frameReference?.VideoMediaFrame?.SoftwareBitmap;
            if (softwareBitmap is null)
                return;

            SoftwareBitmap? converted = null;
            try
            {
                if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                {
                    converted = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                    softwareBitmap = converted;
                }

                var pixels = new byte[softwareBitmap.PixelWidth * softwareBitmap.PixelHeight * 4];
                softwareBitmap.CopyToBuffer(pixels.AsBuffer());

                FrameArrived?.Invoke(this, new CameraFrame(
                    pixels,
                    softwareBitmap.PixelWidth,
                    softwareBitmap.PixelHeight,
                    CameraPixelFormat.Bgra8888,
                    RotationDegrees: 0,
                    frameReference!.SystemRelativeTime ?? TimeSpan.Zero));
            }
            finally
            {
                converted?.Dispose();
            }
        }
    }
}
