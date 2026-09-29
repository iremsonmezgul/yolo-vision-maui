using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using YoloVisionApp.Services;

namespace YoloVisionApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if ANDROID
            builder.Services.AddSingleton<ICameraFrameSource, AndroidCameraFrameSource>();
#elif WINDOWS
            builder.Services.AddSingleton<ICameraFrameSource, WindowsCameraFrameSource>();
#else
            builder.Services.AddSingleton<ICameraFrameSource, UnsupportedCameraFrameSource>();
#endif
            builder.Services.AddSingleton<IObjectDetector, YoloV8Detector>();
            builder.Services.AddTransient<MainPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
