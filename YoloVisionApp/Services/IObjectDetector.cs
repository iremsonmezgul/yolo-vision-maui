using YoloVisionApp.Models;
using YoloVision.Core;

namespace YoloVisionApp.Services
{
    public interface IObjectDetector
    {
        bool IsReady { get; }

        Task InitializeAsync();

        IReadOnlyList<Detection> Detect(CameraFrame frame);
    }
}
