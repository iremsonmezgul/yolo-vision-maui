using YoloVisionApp.Models;

namespace YoloVisionApp.Services
{
    public interface IObjectDetector
    {
        bool IsReady { get; }

        Task InitializeAsync();

        IReadOnlyList<Detection> Detect(CameraFrame frame);
    }
}
