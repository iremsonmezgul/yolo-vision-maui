namespace YoloVision.Core;

public sealed record Detection(
    int ClassId,
    string Label,
    float Confidence,
    float X,
    float Y,
    float Width,
    float Height);