namespace YoloVision.Core;

public static class DetectionMath
{
    public static float IoU(Detection a, Detection b)
    {
        var interWidth = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
        var interHeight = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
        if (interWidth <= 0 || interHeight <= 0)
            return 0;

        var intersection = interWidth * interHeight;
        return intersection / (a.Width * a.Height + b.Width * b.Height - intersection);
    }
}