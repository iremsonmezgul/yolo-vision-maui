namespace YoloVisionApp.Models
{
    public enum CameraPixelFormat
    {
        Rgba8888,
        Bgra8888
    }

    public sealed record CameraFrame(
        byte[] Pixels,
        int Width,
        int Height,
        CameraPixelFormat Format,
        int RotationDegrees,
        TimeSpan Timestamp);
}
