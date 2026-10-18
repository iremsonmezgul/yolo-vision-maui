using YoloVision.Core;

namespace YoloVision.Core.Tests;

public class DetectionMathTests
{
    [Fact]
    public void IoU_IdenticalBoxes_ReturnsOne()
    {
        var box = new Detection(0, "person", 0.9f, X: 0, Y: 0, Width: 10, Height: 10);

        float result = DetectionMath.IoU(box, box);

        Assert.Equal(1f, result);
    }
}