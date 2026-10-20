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

    [Fact]
    public void IoU_SeparateBoxes_ReturnsZero()
    {
        //Arrange
        var first = new Detection(0, "person", 0.9f, X: 0, Y: 0, Width: 10, Height: 10);
        var second = new Detection(0, "person", 0.9f, X: 20, Y: 20, Width: 10, Height: 10);

        //Act
        float result = DetectionMath.IoU(first,second);

        //Assert
        Assert.Equal(0f,result);
    }

    [Fact]
    public void IoU_HalfOverlap_ReturnsOneThird()
    {
        var first = new Detection(0, "person", 0.9f, X: 0, Y: 0, Width: 10, Height: 10);
        var second = new Detection(0, "person", 0.9f, X: 5, Y: 0, Width: 10, Height: 10);

        float result = DetectionMath.IoU(first, second);

        Assert.Equal(1f / 3f, result, 5);
    }

    [Fact]
    public void NonMaxSuppression_OverlappingBoxesSameClass_KeepsHigherConfidence()
    {
        var weak = new Detection(0, "person", 0.6f, X: 0, Y: 0, Width: 10, Height: 10);
        var strong = new Detection(0, "person", 0.9f, X: 1, Y: 1, Width: 10, Height: 10);

        var result = DetectionMath.NonMaxSuppression(new[] { weak, strong }, iouThreshold: 0.45f, maxDetections: 100);

        Assert.Single(result);
        Assert.Equal(strong, result[0]);
    }
}