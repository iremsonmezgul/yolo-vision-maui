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

    [Fact]
    public void NonMaxSuppression_OverlappingBoxesDifferentClass_KeepsBoth() 
    {
        var person = new Detection(0, "person", 0.9f, X: 0, Y: 0, Width: 10, Height: 10);
        var car = new Detection(2, "car",0.8f, X: 1, Y: 1, Width: 10, Height: 10);

        var result = DetectionMath.NonMaxSuppression(new[] { person, car }, iouThreshold: 0.45f, maxDetections: 100);
        
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void NonMaxSuppression_NonOverlappingBoxes_KeepsAll()
    {
        var left = new Detection(0, "person", 0.9f, X: 0, Y: 0, Width: 10, Height: 10);
        var right = new Detection(0, "person", 0.8f, X: 50, Y: 50, Width: 10, Height: 10);

        var result = DetectionMath.NonMaxSuppression(new[] { left, right }, iouThreshold: 0.45f, maxDetections: 100);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void NonMaxSuppression_EmptyInput_ReturnsEmpty()
    {
        var result = DetectionMath.NonMaxSuppression(Array.Empty<Detection>(), iouThreshold: 0.45f, maxDetections: 100);

        Assert.Empty(result);
    }

    [Fact]
    public void NonMaxSuppression_Result_IsSortedByConfidenceDescending()
    {
        var low = new Detection(0, "person", 0.3f, X: 0, Y: 0, Width: 10, Height: 10);
        var high = new Detection(0, "person", 0.9f, X: 50, Y: 0, Width: 10, Height: 10);
        var mid = new Detection(0, "person", 0.6f, X: 100, Y: 0, Width: 10, Height: 10);

        var result = DetectionMath.NonMaxSuppression(new[] { low, high, mid }, iouThreshold: 0.45f, maxDetections: 100);

        Assert.Equal(new[] { high, mid, low }, result);
    }

    [Fact]
    public void IoU_TouchingEdges_ReturnsZero()
    {
        var a = new Detection(0, "person", 1f, X: 0, Y: 0, Width: 10, Height: 10);
        var b = new Detection(0, "person", 1f, X: 10, Y: 0, Width: 10, Height: 10);

        var result = DetectionMath.IoU(a, b);

        Assert.Equal(0f, result);
    }
}