using YoloVisionApp.Models;

namespace YoloVisionApp.Services
{
    public sealed class UnsupportedCameraFrameSource : ICameraFrameSource
    {
        public bool IsRunning => false;

#pragma warning disable CS0067 
        public event EventHandler<CameraFrame>? FrameArrived;
#pragma warning restore CS0067

        public Task StartAsync(CancellationToken cancellationToken = default) =>
            throw new PlatformNotSupportedException("Camera frame streaming is not implemented on this platform yet.");

        public Task StopAsync() => Task.CompletedTask;
    }
}
