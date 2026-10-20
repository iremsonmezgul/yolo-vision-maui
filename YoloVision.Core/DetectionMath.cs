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

    public static List<Detection> NonMaxSuppression(IEnumerable<Detection> candidates, float iouThreshold, int maxDetections)
    {
        var sorted = candidates.OrderByDescending(d => d.Confidence);
        var kept = new List<Detection>();

        foreach (var candidate in sorted)
        {
            if (kept.Count == maxDetections)
            {
                break;
            }

            var overlapsKept = kept.Any(k => k.ClassId == candidate.ClassId && IoU(k, candidate) > iouThreshold);

            if (!overlapsKept)
            {
                kept.Add(candidate);
            }
        }

        return kept;
    }
}