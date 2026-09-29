using YoloVisionApp.Models;

namespace YoloVisionApp.Services
{
    public interface ICameraFrameSource
    {
        bool IsRunning { get; }

        event EventHandler<CameraFrame>? FrameArrived;

        Task StartAsync(CancellationToken cancellationToken = default);

        Task StopAsync();
    }
}
