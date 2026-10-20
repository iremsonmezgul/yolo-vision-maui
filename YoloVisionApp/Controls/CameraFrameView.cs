using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using YoloVisionApp.Models;
using YoloVision.Core;

namespace YoloVisionApp.Controls
{
    public class CameraFrameView : SKCanvasView
    {
        static readonly SKColor[] BoxColors =
        [
            new(0xFF, 0x38, 0x38), new(0xFF, 0x9D, 0x97), new(0xFF, 0x70, 0x1F), new(0xFF, 0xB2, 0x1D),
            new(0xCF, 0xD2, 0x31), new(0x48, 0xF9, 0x0A), new(0x1A, 0x93, 0x34), new(0x00, 0xD4, 0xBB),
            new(0x2C, 0x99, 0xA8), new(0x00, 0xC2, 0xFF), new(0x34, 0x45, 0x93), new(0x64, 0x73, 0xFF),
        ];

        CameraFrame? frame;
        IReadOnlyList<Detection>? detections;
        SKBitmap? bitmap;

        public SKRect FrameRect { get; private set; }

        public CameraFrame? CurrentFrame
        {
            get => frame;
            set
            {
                frame = value;
                InvalidateSurface();
            }
        }

        public IReadOnlyList<Detection>? Detections
        {
            get => detections;
            set
            {
                detections = value;
                InvalidateSurface();
            }
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            base.OnPaintSurface(e);

            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Black);

            var current = frame;
            if (current is null)
                return;

            var source = UpdateBitmap(current);
            var rotation = ((current.RotationDegrees % 360) + 360) % 360;
            var sideways = rotation is 90 or 270;
            var displayWidth = sideways ? source.Height : source.Width;
            var displayHeight = sideways ? source.Width : source.Height;

            var info = e.Info;
            var scale = Math.Min(info.Width / (float)displayWidth, info.Height / (float)displayHeight);
            var drawWidth = displayWidth * scale;
            var drawHeight = displayHeight * scale;
            FrameRect = SKRect.Create((info.Width - drawWidth) / 2, (info.Height - drawHeight) / 2, drawWidth, drawHeight);

            canvas.Save();
            canvas.Translate(FrameRect.MidX, FrameRect.MidY);
            canvas.RotateDegrees(rotation);
            canvas.Scale(scale);
            using var image = SKImage.FromBitmap(source);
            canvas.DrawImage(image, -source.Width / 2f, -source.Height / 2f, new SKSamplingOptions(SKFilterMode.Linear));
            canvas.Restore();

            DrawDetections(canvas, scale, info.Width);
        }

        void DrawDetections(SKCanvas canvas, float scale, int canvasWidth)
        {
            var current = detections;
            if (current is null || current.Count == 0)
                return;

            var density = canvasWidth / (float)Math.Max(1, Width);
            using var boxPaint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 2 * density, IsAntialias = true };
            using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill };
            using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            using var font = new SKFont { Size = 13 * density };

            foreach (var detection in current)
            {
                var rect = SKRect.Create(
                    FrameRect.Left + detection.X * scale,
                    FrameRect.Top + detection.Y * scale,
                    detection.Width * scale,
                    detection.Height * scale);
                var color = BoxColors[detection.ClassId % BoxColors.Length];

                boxPaint.Color = color;
                canvas.DrawRect(rect, boxPaint);

                var text = $"{detection.Label} {detection.Confidence:F2}";
                var padding = 3 * density;
                var textWidth = font.MeasureText(text);
                var labelHeight = font.Size + 2 * padding;
                var labelTop = rect.Top - labelHeight >= FrameRect.Top ? rect.Top - labelHeight : rect.Top;
                fillPaint.Color = color.WithAlpha(0xC0);
                canvas.DrawRect(SKRect.Create(rect.Left, labelTop, textWidth + 2 * padding, labelHeight), fillPaint);
                canvas.DrawText(text, rect.Left + padding, labelTop + padding + font.Size * 0.85f, SKTextAlign.Left, font, textPaint);
            }
        }

        SKBitmap UpdateBitmap(CameraFrame current)
        {
            var colorType = current.Format == CameraPixelFormat.Bgra8888 ? SKColorType.Bgra8888 : SKColorType.Rgba8888;

            if (bitmap is null || bitmap.Width != current.Width || bitmap.Height != current.Height || bitmap.ColorType != colorType)
            {
                bitmap?.Dispose();
                bitmap = new SKBitmap(new SKImageInfo(current.Width, current.Height, colorType, SKAlphaType.Premul));
            }

            var byteCount = Math.Min(current.Pixels.Length, bitmap.ByteCount);
            System.Runtime.InteropServices.Marshal.Copy(current.Pixels, 0, bitmap.GetPixels(), byteCount);
            bitmap.NotifyPixelsChanged();
            return bitmap;
        }
    }
}
